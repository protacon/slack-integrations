using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Slack.Json.Github;
using Slack.Json.Slack;

namespace Slack.Json.Actions
{
    public class ReviewRequestAction: IRequestAction
    {
        private ISlackActionFetcher fetcher;
        private ISlackMessaging slack;
        private ILogger<PullRequestAction> logger;

        public ReviewRequestAction(ISlackActionFetcher fetcher, ISlackMessaging slack, ILogger<PullRequestAction> logger)
        {
            this.fetcher = fetcher;
            this.slack = slack;
            this.logger = logger;
        }

        public string RequestType => "pull_request";
        public string RequestAction => "review_requested";

        public void Execute(JObject request)
        {
            ActionUtils.ParsePullRequestDefaultFields(request, out var repo, out var owner, out var prHtmlUrl, out var prTitle);

            var slackFile = this.fetcher.GetJsonIfAny(owner, repo);

            if (!slackFile.Any())
            {
                this.logger.LogInformation($"Checked review_request for '{owner}/{repo} but no slack.json file defined.'");
                return;
            }

            var reviewers = request["pull_request"]?["requested_reviewers"]?.Value<JArray>()
                    .Select(x => x["login"] ?? throw new InvalidOperationException($"Missing Missing pull_request.requested_reviewers.login"))
                ?? throw new InvalidOperationException($"Missing pull_request.requested_reviewers");

            slackFile
                .Where(slackJsonAction => slackJsonAction.Type == "review_request")
                .ToList()
                .ForEach(action =>
                {
                    this.logger.LogInformation($"Sending message to '{action.Channel}'");
                    this.slack.Send(action.Channel,
                        new SlackMessageModel($"Review request for pull request '{prTitle}'", prHtmlUrl)
                        {
                            Text = $"Review is requested from {string.Join(", ", reviewers)}"
                        });
                });
        }
    }
}