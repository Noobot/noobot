﻿using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Noobot.Domain.Configuration;
using Noobot.Domain.MessagingPipeline;
using Noobot.Domain.MessagingPipeline.Middleware;
using Noobot.Domain.MessagingPipeline.Request;
using Noobot.Domain.MessagingPipeline.Request.Extensions;
using Noobot.Domain.MessagingPipeline.Response;
using SlackConnector;
using SlackConnector.Models;

namespace Noobot.Domain.Slack
{
    public class SlackWrapper : ISlackWrapper
    {
        private readonly IConfigReader _configReader;
        private readonly IPipelineFactory _pipelineFactory;
        private ISlackConnector _client;

        public SlackWrapper(IConfigReader configReader, IPipelineFactory pipelineFactory)
        {
            _configReader = configReader;
            _pipelineFactory = pipelineFactory;
        }

        public async Task Connect()
        {
            JObject config = _configReader.GetConfig();

            _client = new SlackConnector.SlackConnector();
            _client.OnMessageReceived += MessageReceived;
            _client.OnConnectionStatusChanged += ConnectionStatusChanged;

            await _client.Connect(config["slack"].Value<string>("apiToken"));
        }

        private void ConnectionStatusChanged(bool isConnected)
        {
            Console.WriteLine(isConnected ? "CONNECTED :-) x999" : "Bot is no longer connected :-(");
            if (isConnected)
            {
                Console.WriteLine($"Bots Name: {_client.UserName}");
                Console.WriteLine($"Team Name: {_client.TeamName}");
            }
        }

        private async Task MessageReceived(SlackMessage message)
        {
            Console.WriteLine("[[[Message started]]]");

            IMiddleware pipeline = _pipelineFactory.GetPipeline();
            var incomingMessage = new IncomingMessage
            {
                RawText = message.Text,
                FullText = message.Text,
                UserId = message.User.Id,
                Username = GetUsername(message),
                Channel = message.ChatHub.Id,
                UserChannel = await GetUserChannel(message),
                BotName = _client.UserName,
                BotId = _client.UserId,
                BotIsMentioned = message.MentionsBot
            };

            incomingMessage.TargetedText = incomingMessage.GetTargetedText();

            try
            {
                foreach (ResponseMessage responseMessage in pipeline.Invoke(incomingMessage))
                {
                    await SendMessage(responseMessage);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR: {0}", ex);
            }

            Console.WriteLine("[[[Message ended]]]");
        }

        private string GetUsername(SlackMessage message)
        {
            return _client.UserNameCache.ContainsKey(message.User.Id) ? _client.UserNameCache[message.User.Id] : string.Empty;
        }

        private async Task<string> GetUserChannel(SlackMessage message)
        {
            return (await GetUserChatHub(message.User.Id, joinChannel: false) ?? new SlackChatHub()).Id;
        }

        public async Task SendMessage(ResponseMessage responseMessage)
        {
            SlackChatHub chatHub = null;

            if (responseMessage.ResponseType == ResponseType.Channel)
            {
                chatHub = new SlackChatHub { Id = responseMessage.Channel };
            }
            else if (responseMessage.ResponseType == ResponseType.DirectMessage)
            {
                if (string.IsNullOrEmpty(responseMessage.Channel))
                {
                    chatHub = await GetUserChatHub(responseMessage.UserId);
                }
                else
                {
                    chatHub = new SlackChatHub { Id = responseMessage.Channel };
                }
            }

            if (chatHub != null)
            {
                var botMessage = new BotMessage
                {
                    ChatHub = chatHub,
                    Text = responseMessage.Text
                };

                await _client.Say(botMessage);
            }
            else
            {
                Console.WriteLine("Unable to find channel for message '{0}'. Message not sent", responseMessage.Text);
            }
        }

        private async Task<SlackChatHub> GetUserChatHub(string userId, bool joinChannel = true)
        {
            SlackChatHub chatHub = null;

            if (_client.UserNameCache.ContainsKey(userId))
            {
                string username = "@" + _client.UserNameCache[userId];
                chatHub = _client.ConnectedDMs.FirstOrDefault(x => x.Name.Equals(username, StringComparison.InvariantCultureIgnoreCase));
            }

            if (chatHub == null && joinChannel)
            {
                chatHub = await _client.JoinDirectMessageChannel(userId);
            }

            return chatHub;
        }

        public string GetUserIdForUsername(string username)
        {
            var user = _client.UserNameCache.FirstOrDefault(x => x.Value.Equals(username, StringComparison.InvariantCultureIgnoreCase));
            return string.IsNullOrEmpty(user.Key) ? string.Empty : user.Key;
        }

        public string GetChannelId(string channelName)
        {
            var channel = _client.ConnectedChannels.FirstOrDefault(x => x.Name.Equals(channelName, StringComparison.InvariantCultureIgnoreCase));
            return channel != null ? channel.Id : string.Empty;
        }

        public void Disconnect()
        {
            if (_client != null && _client.IsConnected)
            {
                _client.Disconnect();
            }
        }
    }
}