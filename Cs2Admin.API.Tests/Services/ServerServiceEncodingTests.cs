using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Cs2Admin.API.Tests.Services;

public class ServerServiceEncodingTests
{
    [Fact]
    public async Task FileWrite_WithUtf8WithoutBom_DoesNotIncludePreamble()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var encodingWithoutBom = new UTF8Encoding(false);
            var content = "hostname \"CS2 Server\"\nexec multiaddonmanager.cfg";

            await File.WriteAllTextAsync(tempFile, content, encodingWithoutBom);

            var rawBytes = await File.ReadAllBytesAsync(tempFile);

            // Assert that the first three bytes are NOT the UTF-8 BOM preamble (0xEF, 0xBB, 0xBF)
            var bom = Encoding.UTF8.GetPreamble(); // 0xEF, 0xBB, 0xBF
            Assert.True(rawBytes.Length >= bom.Length);
            Assert.False(rawBytes[0] == bom[0] && rawBytes[1] == bom[1] && rawBytes[2] == bom[2],
                "File contains UTF-8 BOM which breaks plugin config parsing!");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
