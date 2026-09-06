using System;
using System.IO;
using IntelliTrader.Core;
using IntelliTrader.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace IntelliTrader.Trading.Tests
{
    public class HomeControllerTests
    {
        [Fact]
        public void DownloadLog_ReturnsNotFound_WhenLogFileDoesNotExist()
        {
            // Arrange
            var controller = new HomeController();
            string tempDir = Path.Combine(Directory.GetCurrentDirectory(), "log");

            // Ensure directory exists or create temporary directory context if needed
            if (!Directory.Exists(tempDir))
            {
                Directory.CreateDirectory(tempDir);
            }

            // Act
            var result = controller.DownloadLog("nonexistent_log_type");

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Equal("Log file not found.", notFoundResult.Value);
        }

        [Fact]
        public void DownloadLog_ReturnsFileStreamResult_WhenLogFileExists()
        {
            // Arrange
            var controller = new HomeController();
            string logsPath = Path.Combine(Directory.GetCurrentDirectory(), "log");
            if (!Directory.Exists(logsPath))
            {
                Directory.CreateDirectory(logsPath);
            }

            string testLogFile = Path.Combine(logsPath, $"{DateTime.Now:yyyy-MM-dd}-general.txt");
            File.WriteAllText(testLogFile, "Log line 1\nLog line 2\n");

            try
            {
                // Act
                var result = controller.DownloadLog("general");

                // Assert
                var fileResult = Assert.IsType<FileStreamResult>(result);
                Assert.Equal("text/plain", fileResult.ContentType);
                Assert.Equal(Path.GetFileName(testLogFile), fileResult.FileDownloadName);

                fileResult.FileStream.Dispose();
            }
            finally
            {
                if (File.Exists(testLogFile))
                {
                    File.Delete(testLogFile);
                }
            }
        }
    }
}
