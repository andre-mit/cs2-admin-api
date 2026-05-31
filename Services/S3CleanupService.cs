using Amazon.S3;
using Amazon.S3.Model;
using StackExchange.Redis;

namespace Cs2Admin.API.Services
{
    public class S3CleanupService : BackgroundService
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly IAmazonS3 _s3;
        private readonly IConfiguration _config;
        private readonly ILogger<S3CleanupService> _logger;

        public S3CleanupService(IConnectionMultiplexer redis, IAmazonS3 s3, IConfiguration config, ILogger<S3CleanupService> logger)
        {
            _redis = redis;
            _s3 = s3;
            _config = config;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromHours(12), stoppingToken);
                    await CleanupOrphanedUploads();
                }
            }
            catch (OperationCanceledException ex)
            {
                _logger.LogWarning(ex, "S3 Cleanup Service was cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "S3 Cleanup Service failed");
            }
        }

        private async Task CleanupOrphanedUploads()
        {
            var db = _redis.GetDatabase();
            var cutoff = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds();
            var expired = await db.SortedSetRangeByScoreAsync("pending_uploads", double.NegativeInfinity, cutoff);
            var bucket = _config["S3:BucketName"] ?? "cs2";

            foreach (var entry in expired)
            {
                var key = entry.ToString();
                try
                {
                    await _s3.DeleteObjectAsync(new DeleteObjectRequest { BucketName = bucket, Key = key });
                    await db.SortedSetRemoveAsync("pending_uploads", key);
                    _logger.LogInformation("Deleted orphaned S3 object: {Key}", key);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete orphaned S3 object: {Key}", key);
                }
            }
        }
    }
}
