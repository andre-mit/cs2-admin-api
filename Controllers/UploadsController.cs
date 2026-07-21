using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;

using Microsoft.AspNetCore.Authorization;

namespace Cs2Admin.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/v1/[controller]")]
    public class UploadsController(IAmazonS3 s3, IConnectionMultiplexer redis, IConfiguration config) : ControllerBase
    {

        [HttpGet("presigned-url")]
        public async Task<IActionResult> GetPresignedUrl(
            [FromQuery] string fileName, 
            [FromQuery] string contentType,
            [FromQuery] string mapName,
            [FromQuery] string imageType)
        {
            var bucket = config["S3:BucketName"] ?? "cs2";
            
            var sanitizedMapName = string.IsNullOrWhiteSpace(mapName) 
                ? "unnamed" 
                : mapName.ToLower().Replace(" ", "_");
            
            var sanitizedImageType = string.IsNullOrWhiteSpace(imageType) 
                ? "background" 
                : imageType.ToLower();

            var extension = Path.GetExtension(fileName) ?? ".jpg";
            var key = $"maps/{sanitizedMapName}/{sanitizedImageType}{extension}";

            // Ensure the bucket policy allows public anonymous reads of images
            try
            {
                var policy = $@"{{
                    ""Version"": ""2012-10-17"",
                    ""Statement"": [
                        {{
                            ""Sid"": ""PublicReadGetObject"",
                            ""Effect"": ""Allow"",
                            ""Principal"": ""*"",
                            ""Action"": ""s3:GetObject"",
                            ""Resource"": ""arn:aws:s3:::{bucket}/*""
                        }}
                    ]
                }}";

                await s3.PutBucketPolicyAsync(new PutBucketPolicyRequest
                {
                    BucketName = bucket,
                    Policy = policy
                });
            }
            catch
            {
                // Silently skip if bucket policy setting is restricted
            }

            var request = new GetPreSignedUrlRequest
            {
                BucketName = bucket,
                Key = key,
                Verb = HttpVerb.PUT,
                Expires = DateTime.UtcNow.AddMinutes(15),
                ContentType = contentType
            };

            var presignedUrl = await s3.GetPreSignedURLAsync(request);

            var db = redis.GetDatabase();
            var score = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await db.SortedSetAddAsync("pending_uploads", key, score);

            var serviceUrl = config["S3:ServiceUrl"] ?? "";
            var publicUrl = $"{serviceUrl}/{bucket}/{key}";

            return Ok(new { uploadUrl = presignedUrl, publicUrl, s3Key = key });
        }

        [HttpPost]
        public async Task<IActionResult> UploadFile(
            IFormFile file,
            [FromQuery] string mapName,
            [FromQuery] string imageType)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file uploaded.");
            }

            var bucket = config["S3:BucketName"] ?? "cs2";
            
            var sanitizedMapName = string.IsNullOrWhiteSpace(mapName) 
                ? "unnamed" 
                : mapName.ToLower().Replace(" ", "_");
            
            var sanitizedImageType = string.IsNullOrWhiteSpace(imageType) 
                ? "background" 
                : imageType.ToLower();

            var extension = Path.GetExtension(file.FileName) ?? ".jpg";
            var key = $"maps/{sanitizedMapName}/{sanitizedImageType}{extension}";

            // Ensure the bucket policy allows public anonymous reads of images
            try
            {
                var policy = $@"{{
                    ""Version"": ""2012-10-17"",
                    ""Statement"": [
                        {{
                            ""Sid"": ""PublicReadGetObject"",
                            ""Effect"": ""Allow"",
                            ""Principal"": ""*"",
                            ""Action"": ""s3:GetObject"",
                            ""Resource"": ""arn:aws:s3:::{bucket}/*""
                        }}
                    ]
                }}";

                await s3.PutBucketPolicyAsync(new PutBucketPolicyRequest
                {
                    BucketName = bucket,
                    Policy = policy
                });
            }
            catch
            {
                // Silently skip if bucket policy setting is restricted
            }

            try
            {
                using (var stream = file.OpenReadStream())
                {
                    var request = new PutObjectRequest
                    {
                        BucketName = bucket,
                        Key = key,
                        InputStream = stream,
                        ContentType = file.ContentType
                    };

                    await s3.PutObjectAsync(request);
                }

                var db = redis.GetDatabase();
                var score = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                await db.SortedSetAddAsync("pending_uploads", key, score);

                var serviceUrl = config["S3:ServiceUrl"] ?? "";
                var publicUrl = $"{serviceUrl}/{bucket}/{key}";

                return Ok(new { publicUrl, s3Key = key });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
    }
}
