﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Bot.Schema;
using Newtonsoft.Json;
using Xunit;

namespace Microsoft.Bot.Builder.Adapters.Twilio.Tests
{
    public class TwilioAdapterTests
    {
        [Fact]
        public void Constructor_Should_Fail_With_Null_Options()
        {
            Assert.Throws<ArgumentNullException>(() => { new TwilioAdapter(null); });
        }

        [Fact]
        public void Constructor_Should_Fail_With_Null_TwilioNumber()
        {
            ITwilioAdapterOptions options = new MockTwilioOptions
            {
                TwilioNumber = null,
                AccountSid = "Test",
                AuthToken = "Test",
            };

            Assert.Throws<Exception>(() => { new TwilioAdapter(options); });
        }

        [Fact]
        public void Constructor_Should_Fail_With_Null_AccountSid()
        {
            ITwilioAdapterOptions options = new MockTwilioOptions
            {
                TwilioNumber = "Test",
                AccountSid = null,
                AuthToken = "Test",
            };

            Assert.Throws<Exception>(() => { new TwilioAdapter(options); });
        }

        [Fact]
        public void Constructor_Should_Fail_With_Null_AuthToken()
        {
            ITwilioAdapterOptions options = new MockTwilioOptions
            {
                TwilioNumber = "Test",
                AccountSid = "Test",
                AuthToken = null,
            };

            Assert.Throws<Exception>(() => { new TwilioAdapter(options); });
        }

        [Fact]
        public void Constructor_WithArguments_Succeeds()
        {
            ITwilioAdapterOptions options = new MockTwilioOptions
            {
                TwilioNumber = "Test",
                AccountSid = "Test",
                AuthToken = "Test",
            };

            Assert.NotNull(new TwilioAdapter(options));
        }

        [Fact]
        public void ActivityToTwilio_Should_Return_MessageOptions_With_MediaUrl()
        {
            ITwilioAdapterOptions options = new MockTwilioOptions
            {
                TwilioNumber = "Test",
                AccountSid = "Test",
                AuthToken = "Test",
            };

            var adapter = new TwilioAdapter(options);
            var activity = JsonConvert.DeserializeObject<Activity>(File.ReadAllText(Directory.GetCurrentDirectory() + @"\files\Activities.json"));
            var activities = new Activity[] { activity };
            activity.Attachments = new List<Attachment> { new Attachment(contentUrl: "http://example.com") };
            var messageOption = TwilioHelper.ActivityToTwilio(activity, "123456789");

            Assert.Equal(activity.Conversation.Id, messageOption.ApplicationSid);
            Assert.Equal("123456789", messageOption.From.ToString());
            Assert.Equal(activity.Text, messageOption.Body);
            Assert.Equal(new Uri(activity.Attachments[0].ContentUrl), messageOption.MediaUrl[0]);
        }

        private class MockTwilioOptions : ITwilioAdapterOptions
        {
            public string TwilioNumber { get; set; }

            public string AccountSid { get; set; }

            public string AuthToken { get; set; }

            public string ValidationUrl { get; set; }
        }
    }
}
