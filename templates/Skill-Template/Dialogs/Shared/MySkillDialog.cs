﻿using $safeprojectname$.Dialogs.Shared.Resources;
using Luis;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Bot.Schema;
using Microsoft.Bot.Solutions;
using Microsoft.Bot.Solutions.Extensions;
using Microsoft.Bot.Solutions.Skills;
using Microsoft.Recognizers.Text;
using Newtonsoft.Json.Linq;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace $safeprojectname$
{
    public class $safeprojectname$Dialog : ComponentDialog
    {
        // Constants
        public const string SkillModeAuth = "SkillAuth";
        public const string LocalModeAuth = "LocalAuth";

        // Fields
        protected SkillConfiguration _services;
        protected IStatePropertyAccessor<$safeprojectname$State> _accessor;
        protected IServiceManager _serviceManager;
        protected $safeprojectname$ResponseBuilder _responseBuilder = new $safeprojectname$ResponseBuilder();

        public $safeprojectname$Dialog(
            string dialogId,
            SkillConfiguration services,
            IStatePropertyAccessor<$safeprojectname$State> accessor,
            IServiceManager serviceManager)
            : base(dialogId)
        {
            _services = services;
            _accessor = accessor;
            _serviceManager = serviceManager;

            var oauthSettings = new OAuthPromptSettings()
            {
                ConnectionName = _services.AuthConnectionName ?? throw new Exception("The authentication connection has not been initialized."),
                Text = $"Authentication",
                Title = "Signin",
                Timeout = 300000, // User has 5 minutes to login
            };

            AddDialog(new EventPrompt(SkillModeAuth, "tokens/response", TokenResponseValidator));
            AddDialog(new OAuthPrompt(LocalModeAuth, oauthSettings, AuthPromptValidator));
        }

        protected override async Task<DialogTurnResult> OnBeginDialogAsync(DialogContext dc, object options, CancellationToken cancellationToken = default(CancellationToken))
        {
            var state = await _accessor.GetAsync(dc.Context);
            await DigestLuisResult(dc, state.LuisResult);
            return await base.OnBeginDialogAsync(dc, options, cancellationToken);
        }

        protected override async Task<DialogTurnResult> OnContinueDialogAsync(DialogContext dc, CancellationToken cancellationToken = default(CancellationToken))
        {
            var state = await _accessor.GetAsync(dc.Context);
            await DigestLuisResult(dc, state.LuisResult);
            return await base.OnContinueDialogAsync(dc, cancellationToken);
        }

        // Shared steps
        public async Task<DialogTurnResult> GetAuthToken(WaterfallStepContext sc, CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                var skillOptions = ($safeprojectname$DialogOptions)sc.Options;

                // If in Skill mode we ask the calling Bot for the token
                if (skillOptions != null && skillOptions.SkillMode)
                {
                    // We trigger a Token Request from the Parent Bot by sending a "TokenRequest" event back and then waiting for a "TokenResponse"
                    // TODO Error handling - if we get a new activity that isn't an event
                    var response = sc.Context.Activity.CreateReply();
                    response.Type = ActivityTypes.Event;
                    response.Name = "tokens/request";

                    // Send the tokens/request Event
                    await sc.Context.SendActivityAsync(response);

                    // Wait for the tokens/response event
                    return await sc.PromptAsync(SkillModeAuth, new PromptOptions());
                }
                else
                {
                    return await sc.PromptAsync(LocalModeAuth, new PromptOptions());
                }
            }
            catch(Exception ex)
            {
                throw await HandleDialogExceptions(sc, ex);
            }
        }

        public async Task<DialogTurnResult> AfterGetAuthToken(WaterfallStepContext sc, CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                // When the user authenticates interactively we pass on the tokens/Response event which surfaces as a JObject
                // When the token is cached we get a TokenResponse object.
                var skillOptions = ($safeprojectname$DialogOptions)sc.Options;
                TokenResponse tokenResponse;
                if (skillOptions != null && skillOptions.SkillMode)
                {
                    var resultType = sc.Context.Activity.Value.GetType();
                    if (resultType == typeof(TokenResponse))
                    {
                        tokenResponse = sc.Context.Activity.Value as TokenResponse;
                    }
                    else
                    {
                        var tokenResponseObject = sc.Context.Activity.Value as JObject;
                        tokenResponse = tokenResponseObject?.ToObject<TokenResponse>();
                    }
                }
                else
                {
                    tokenResponse = sc.Result as TokenResponse;
                }

                if (tokenResponse != null)
                {
                    var state = await _accessor.GetAsync(sc.Context);
                }

                return await sc.NextAsync();
            }
            catch(Exception ex)
            {
                throw await HandleDialogExceptions(sc, ex);
            }
        }    

        // Validators
        private Task<bool> TokenResponseValidator(PromptValidatorContext<Activity> pc, CancellationToken cancellationToken)
        {
            var activity = pc.Recognized.Value;
            if (activity != null && activity.Type == ActivityTypes.Event)
            {
                return Task.FromResult(true);
            }
            else
            {
                return Task.FromResult(false);
            }
        }

        private Task<bool> AuthPromptValidator(PromptValidatorContext<TokenResponse> pc, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

     
        // Helpers
        public async Task DigestLuisResult(DialogContext dc, Skill luisResult)
        {
            try
            {
                var state = await _accessor.GetAsync(dc.Context);

                // extract entities and store in state here.
            }
            catch
            {
                // put log here
            }
        }

        // This method is called by any waterfall step that throws an exception to ensure consistency
        public async Task<Exception> HandleDialogExceptions(WaterfallStepContext sc, Exception ex)
        {
            await sc.Context.SendActivityAsync(sc.Context.Activity.CreateReply($safeprojectname$SharedResponses.ErrorMessage));
            await sc.CancelAllDialogsAsync();
            return ex;
        }
    }
}
