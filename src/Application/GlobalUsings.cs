global using Application.Common;
global using Application.Features;
global using Application.Infrastructure.OpenAI;
global using Application.Infrastructure.Pubg;
global using Application.Infrastructure.Twitch;
global using MediatR;
global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Hosting;
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Options;
global using OpenAI;
global using OpenAI.Audio;
global using System.Text.RegularExpressions;
global using TwitchLib.Client.Models;
global using TwitchLib.EventSub.Websockets;
global using TwitchLib.EventSub.Websockets.Core.EventArgs;
global using TwitchLib.EventSub.Websockets.Core.EventArgs.Channel;
global using TwitchLib.EventSub.Websockets.Extensions;
global using TwitchLib.PubSub.Models.Responses.Messages.Redemption;