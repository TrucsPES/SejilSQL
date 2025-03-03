// Copyright (C) 2017 Alaa Masoud
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Moq;
using SejilSQL.Configuration;
using SejilSQL.Data.Internal;
using SejilSQL.Models.Internal;
using Xunit;
using Xunit.Abstractions;
using Dapper;
using System.Linq;
using Serilog.Events;
using System.IO;

namespace SejilSQL.Test.Configuration
{
    public class SejilSettingsTests
    {
        [Fact]
        public void Ctor_loads_app_html()
        {
            // Arrange & act
            var settings = new SejilSettings("url", LogEventLevel.Debug);

            // Assert
            Assert.Equal(ResourceHelper.GetEmbeddedResource("SejilSQL.index.html"), settings.SejilAppHtml);
        }

        [Fact]
        public void Ctor_save_and_adds_a_leading_slash_to_specified_url_when_missing()
        {
            // Arrange & act
            var settings = new SejilSettings("url", LogEventLevel.Debug);

            // Assert
            Assert.Equal("/url", settings.Url);
        }

        [Fact]
        public void Ctor_saves_specified_url()
        {
            // Arrange & act
            var settings = new SejilSettings("/url", LogEventLevel.Debug);

            // Assert
            Assert.Equal("/url", settings.Url);
        }

        [Fact]
        public void Ctor_sets_inital_min_log_level()
        {
            // Arrange & act
            var initalLogLevel = LogEventLevel.Debug;
            var settings = new SejilSettings("/url", initalLogLevel);

            // Assert
            Assert.Equal(initalLogLevel, settings.LoggingLevelSwitch.MinimumLevel);
        }

        [Fact]
        public void Ctor_sets_default_settings()
        {
            // Arrange & act
            var settings = new SejilSettings("", LogEventLevel.Debug);

            // Assert
            Assert.Equal(new[] { "message", "level", "timestamp", "exception", "sourceapp" },
                settings.NonPropertyColumns);
            Assert.Equal(100, settings.PageSize);
        }

        [Theory]
        [InlineData("Trace", LogEventLevel.Verbose, true)]
        [InlineData("verbose", LogEventLevel.Verbose, true)]
        [InlineData("DEBUG", LogEventLevel.Debug, true)]
        [InlineData("information", LogEventLevel.Information, true)]
        [InlineData("Warning", LogEventLevel.Warning, true)]
        [InlineData("Error", LogEventLevel.Error, true)]
        [InlineData("Critical", LogEventLevel.Fatal, true)]
        [InlineData("faTAL", LogEventLevel.Fatal, true)]
        [InlineData("none", (LogEventLevel)0, false)]
        public void TrySetMinimumLogLevel_attempts_to_sets_specified_min_log_level(string logLevel, LogEventLevel expected, bool expectedResult)
        {
            // Arrange
            var initialLogLevel = LogEventLevel.Debug;
            var settings = new SejilSettings("", initialLogLevel);

            // Act
            var result = settings.TrySetMinimumLogLevel(logLevel);

            // Assert
            Assert.Equal(expectedResult, result);
            if (result)
            {
                Assert.Equal(expected, settings.LoggingLevelSwitch.MinimumLevel);
            }
            else
            {
                Assert.Equal(initialLogLevel, settings.LoggingLevelSwitch.MinimumLevel);
            }
        }
    }
}