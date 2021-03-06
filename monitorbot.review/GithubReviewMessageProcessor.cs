﻿using System;
using monitorbot.core.utils;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using monitorbot.core.bot;
using monitorbot.review.reviewer;
using monitorbot.review.services;

namespace monitorbot.review
{
    internal class GithubReviewMessageProcessor : IMessageProcessor
    {
        private readonly ICommandParser m_CommandParser;
        private readonly string m_DefaultRepo;
        private readonly string m_DefaultUser;
        private readonly IReviewApi m_ReviewApi;

        public GithubReviewMessageProcessor(ICommandParser commandParser, IReviewApi review, string defaultUser = null, string defaultRepo = null)
        {
            m_CommandParser = commandParser;
            m_ReviewApi = review;
            m_DefaultUser = defaultUser;
            m_DefaultRepo = defaultRepo;
        }

        public MessageResult ProcessMessage(Message message)
        {
            string toReview;
            if (m_CommandParser.TryGetCommand(message, "review", out toReview))
            {
                var reference = GithubReferenceParser.Parse(toReview);
                if (reference == null) return MessageResult.Empty;
                var user = reference.User ?? m_DefaultUser;
                var repo = reference.Repo ?? m_DefaultRepo;
                IEnumerable<DiffComment> comments = null;
                if (reference.Commit != null)
                {
                    comments = m_ReviewApi.ReviewForCommit(user, repo, reference.Commit).Result;
                }
                if (reference.BaseBranch != null)
                {
                    comments = m_ReviewApi.ReviewForComparison(user, repo, reference.BaseBranch + "..." + reference.Branch).Result;
                }
                if (reference.Branch == "master")
                {
                    comments = m_ReviewApi.ReviewForComparison(user, repo, "master@{1day}..." + reference.Branch).Result;
                }
                if (reference.Branch != null)
                {
                    comments = m_ReviewApi.ReviewForComparison(user, repo, "master..." + reference.Branch).Result;
                }
                if (reference.Issue > 0)
                {
                    comments = m_ReviewApi.ReviewForPullRequest(user, repo, reference.Issue).Result;
                }

                if (comments != null)
                {
                    return FormatComments(message, comments);
                }
            }
            return MessageResult.Empty;
        }

        private MessageResult FormatComments(Message message, IEnumerable<DiffComment> comments)
        {
            if (!comments.Any())
            {
                return Response.ToMessage(message, "No issues detected. Looks good!");
            }
            var responses = Group(comments).Take(20).Select(x => Response.ToMessage(message, x)).ToList();
            return new MessageResult(responses);
        }

        private IEnumerable<string> Group(IEnumerable<DiffComment> comments)
        {
            var groupedByType = comments.GroupBy(x => x.Description);
            return groupedByType.Select(x => "*" + x.Key + ":* " + String.Join(", ", GetFirstThree(GroupByFile(x))));
        }

        private IEnumerable<string> GroupByFile(IEnumerable<DiffComment> comments)
        {
            var groupdByFile = comments.GroupBy(x => x.File);
            return groupdByFile.Select(x => x.Key + ": (" + String.Join(", ", GetFirstThree(GroupByLine(x))) + ")");
        }

        private IEnumerable<string> GroupByLine(IEnumerable<DiffComment> comments)
        {
            var groupedByLine = comments.GroupBy(x => x.Line);
            // TODO: actually link to lines
            return groupedByLine.Select(x => string.Format("L{0}", x.Key.ToString()));
        }

        private IEnumerable<string> GetFirstThree(IEnumerable<string> input)
        {
            var firstThree = input.Take(3);
            if (input.ElementAtOrDefault(4) != null)
            {
                firstThree = firstThree.Concat(new[] { "..." });
            }
            return firstThree;
        }

        public MessageResult ProcessTimerTick()
        {
            return MessageResult.Empty;
        }
    }
}