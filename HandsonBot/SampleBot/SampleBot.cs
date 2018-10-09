using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;

namespace HandsonBot.SampleBot
{
    public class SampleBot : IBot
    {
        private const string WelcomeText = "SampleBot へようこそ！";

        private readonly ILogger _logger;
        private readonly SampleBotAccessors _accessors; // Added
        private readonly DialogSet _dialogs; // Added

        public SampleBot(SampleBotAccessors accessors, ILoggerFactory loggerFactory) // Updated
        {
            _accessors = accessors ?? throw new ArgumentException(nameof(accessors)); // Added

            _dialogs = new DialogSet(accessors.ConversationDialogState); // Added
            _dialogs.Add(new TextPrompt("name", ValidateHandleNameAsync));

            _logger = loggerFactory.CreateLogger<SampleBot>();
            _logger.LogInformation("Start SampleBot");
        }

        private Task<bool> ValidateHandleNameAsync(PromptValidatorContext<string> promptContext, CancellationToken cancellationToken)
        {
            var result = promptContext.Recognized.Value;

            if (result != null && result.Length >= 3)
            {
                var upperValue = result.ToUpperInvariant();
                promptContext.Recognized.Value = upperValue;
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        public async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (turnContext.Activity.Type == ActivityTypes.Message)
            {
                // 通常のメッセージのやり取りはここで行います。
                await SendMessageActivityAsync(turnContext, cancellationToken); // updated
            }
            else if (turnContext.Activity.Type == ActivityTypes.ConversationUpdate)
            {
                await SendWelcomeMessageAsync(turnContext, cancellationToken);
            }
            else
            {
                _logger.LogInformation($"passed:{turnContext.Activity.Type}");
            }

            await _accessors.ConversationState.SaveChangesAsync(turnContext, false, cancellationToken);
            await _accessors.UserState.SaveChangesAsync(turnContext, false, cancellationToken);
        }

        private static async Task SendWelcomeMessageAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            foreach (var member in turnContext.Activity.MembersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    await turnContext.SendActivityAsync(WelcomeText, cancellationToken: cancellationToken);
                }
            }
        }

        private async Task SendMessageActivityAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            var dialogContext = await _dialogs.CreateContextAsync(turnContext, cancellationToken);
            var dialogTurnResult = await dialogContext.ContinueDialogAsync(cancellationToken);

            var userProfile = await _accessors.UserProfile.GetAsync(turnContext, () => new UserProfile(), cancellationToken);

            // ハンドルネームを UserState に未登録の場合
            if (userProfile.HandleName == null)
            {
                await GetHandleNameAsync(dialogContext, dialogTurnResult, userProfile, cancellationToken);
            }
            else
            {
                await turnContext.SendActivityAsync($"こんにちは、{userProfile.HandleName}", cancellationToken: cancellationToken);
            }
        }

        private async Task GetHandleNameAsync(DialogContext dialogContext, DialogTurnResult dialogTurnResult, UserProfile userProfile, CancellationToken cancellationToken)
        {
            if (dialogTurnResult.Status is DialogTurnStatus.Empty)
            {
                await dialogContext.PromptAsync(
                    "name",
                    new PromptOptions
                    {
                        Prompt = MessageFactory.Text("最初にハンドルネームを教えてください。"),
                        RetryPrompt = MessageFactory.Text("ハンドルネームは3文字以上入力してください。"),
                    },
                    cancellationToken);
            }
            else if (dialogTurnResult.Status is DialogTurnStatus.Complete)
            {
                // ハンドルネームを UserState に登録
                userProfile.HandleName = (string)dialogTurnResult.Result;
                _logger.LogInformation($"ハンドルネーム登録なう: {userProfile.HandleName}");
            }
        }
    }
}
