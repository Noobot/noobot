﻿using System.Collections.Generic;
using Noobot.Custom.Plugins;
using Noobot.Domain.MessagingPipeline.Middleware;
using Noobot.Domain.MessagingPipeline.Request;
using Noobot.Domain.MessagingPipeline.Response;

namespace Noobot.Custom.Pipeline.Middleware
{
    public class PingMiddleware : MiddlewareBase
    {
        private readonly PingPlugin _pingPlugin;

        public PingMiddleware(IMiddleware next, PingPlugin pingPlugin) : base(next)
        {
            _pingPlugin = pingPlugin;

            HandlerMappings = new[]
            {
                new HandlerMapping
                {
                    ValidHandles = new []{ "/ping stop", "stop pinging me" },
                    Description = "Stops sending you pings",
                    EvaluatorFunc = StopPingingHandler
                },
                new HandlerMapping
                {
                    ValidHandles = new []{ "/ping list", "ping list" },
                    Description = "Lists all of the people currently being pinged",
                    EvaluatorFunc = ListPingHandler
                },
                new HandlerMapping
                {
                    ValidHandles = new []{ "ping me", "/ping" },
                    Description = "Sends you a ping about every second",
                    EvaluatorFunc = PingHandler
                },
            };
        }

        private IEnumerable<ResponseMessage> PingHandler(IncomingMessage message)
        {
            yield return message.ReplyToChannel("Ok, I will start pinging @" + message.Username);
            _pingPlugin.StartPingingUser(message.UserId);
        }

        private IEnumerable<ResponseMessage> StopPingingHandler(IncomingMessage message)
        {
            if (_pingPlugin.StopPingingUser(message.UserId))
            {
                yield return message.ReplyToChannel("Ok, I will stop pinging @" + message.Username);
            }
            else
            {
                yield return message.ReplyToChannel("BUT I AM NOT PINGING @" + message.Username);
            }
        }

        private IEnumerable<ResponseMessage> ListPingHandler(IncomingMessage message)
        {
            string[] users = _pingPlugin.ListPingedUsers();

            yield return message.ReplyDirectlyToUser("I am currently pinging:");
            yield return message.ReplyDirectlyToUser(">>>" + string.Join("\n", users));
        }
    }
}