using System;
using System.IO;
using System.Text;
using IntelliTrader.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace IntelliTrader.Trading.Tests
{
    public class HomeControllerTests
    {
        [Fact]
        public void DownloadLog_ReturnsNotFound_WhenNoMatchingFile()
        {
            // Arrange
            var controller = new HomeController();

            // Act
            var result = controller.DownloadLog("nonexistent_log_type_12345");

            // Assert
            Assert.IsType<NotFoundObjectResult>(result);
            var notFoundResult = (NotFoundObjectResult)result;
            Assert.Equal("Log file not found.", notFoundResult.Value);
        }

        [Fact]
        public void DownloadLog_ReturnsFileStreamResult_WhenGeneralLogExists()
        {
            // Arrange
            var controller = new HomeController();
            string logDirectory = Path.Combine(Directory.GetCurrentDirectory(), "log");
            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }

            string testFileName = $"{DateTime.Now:yyyy-MM-dd-HHmmss}-test-general.txt";
            string testFilePath = Path.Combine(logDirectory, testFileName);
            string testContent = "[INF] 2026-08-12 10:00:00 Test log entry for DownloadLog test.";

            try
            {
                File.WriteAllText(testFilePath, testContent, Encoding.UTF8);

                // Act
                var result = controller.DownloadLog("general");

                // Assert
                Assert.IsType<FileStreamResult>(result);
                var fileResult = (FileStreamResult)result;
                Assert.Equal("text/plain", fileResult.ContentType);
                Assert.False(string.IsNullOrEmpty(fileResult.FileDownloadName));
                Assert.EndsWith("-general.txt", fileResult.FileDownloadName);

                using (var reader = new StreamReader(fileResult.FileStream, Encoding.UTF8))
                {
                    string content = reader.ReadToEnd();
                    Assert.Contains("Test log entry for DownloadLog test.", content);
                }
            }
            finally
            {
                if (File.Exists(testFilePath))
                {
                    File.Delete(testFilePath);
                }
            }
        }

        [Fact]
        public void DownloadLog_ReturnsFileStreamResult_WhenTradesLogExists()
        {
            // Arrange
            var controller = new HomeController();
            string logDirectory = Path.Combine(Directory.GetCurrentDirectory(), "log");
            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }

            string testFileName = $"{DateTime.Now:yyyy-MM-dd-HHmmss}-test-trades.txt";
            string testFilePath = Path.Combine(logDirectory, testFileName);
            string testContent = "TradeResult {\"Pair\":\"BTCUSDT\",\"Profit\":10.5}";

            try
            {
                File.WriteAllText(testFilePath, testContent, Encoding.UTF8);

                // Act
                var result = controller.DownloadLog("trades");

                // Assert
                Assert.IsType<FileStreamResult>(result);
                var fileResult = (FileStreamResult)result;
                Assert.Equal("text/plain", fileResult.ContentType);
                Assert.False(string.IsNullOrEmpty(fileResult.FileDownloadName));
                Assert.EndsWith("-trades.txt", fileResult.FileDownloadName);

                using (var reader = new StreamReader(fileResult.FileStream, Encoding.UTF8))
                {
                    string content = reader.ReadToEnd();
                    Assert.Contains("TradeResult", content);
                }
            }
            finally
            {
                if (File.Exists(testFilePath))
                {
                    File.Delete(testFilePath);
                }
            }
        }
    }
}
