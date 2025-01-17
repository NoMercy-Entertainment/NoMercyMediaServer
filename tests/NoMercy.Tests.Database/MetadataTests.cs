
using Xunit;
using NoMercy.Database.Models;

namespace NoMercy.Tests.Database;

public class MetadataTests
{
    [Fact]
    public async Task CalculateTotalSize_ReturnsCorrectSize()
    {
        // Arrange
        Metadata? metadata = new()
        {
            Id = new(),
            Video =
            [
                new() { FileSize = 500 },
                new() { FileSize = 700 }
            ],
            Audio =
            [
                new() { FileSize = 300 },
                new() { FileSize = 400 }
            ],
            Subtitles =
            [
                new() { FileSize = 50 },
                new() { FileSize = 70 }
            ],
            Previews =
            [
                new() { ImageFileSize = 30, TimeFileSize = 20 },
                new() { ImageFileSize = 40, TimeFileSize = 25 }
            ],
            Fonts =
            [
                new() { FileSize = 15 },
                new() { FileSize = 25 }
            ],
            FontsFile = new() { FileSize = 100 },
            Chapters = new() { FileSize = 200 }
        };

        // Expected total size
        long expectedTotalSize = 
            500 + 700 // Video sizes
                + 300 + 400 // Audio sizes
                + 50 + 70   // Subtitle sizes
                + (30 + 20) + (40 + 25) // Preview sizes
                + 15 + 25   // Font sizes
                + 100       // FontsFile size
                + 200;      // Chapters size

        // Act
        long totalSize = metadata.CalculateTotalSize();

        // Assert
        Assert.Equal(expectedTotalSize, totalSize);
        
        await Task.CompletedTask;
    }
}