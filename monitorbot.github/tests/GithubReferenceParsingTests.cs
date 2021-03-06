﻿using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using monitorbot.review.services;

namespace monitorbot.review.tests
{
    class GithubReferenceParsingTests
    {
        [Test]
        public void ParsesFullCommitReference()
        {
            var gref = GithubReferenceParser.Parse("fooCorp/fooRepo@abc1235");
            Assert.AreEqual("fooCorp", gref.User);
            Assert.AreEqual("fooRepo", gref.Repo);
            Assert.AreEqual("abc1235", gref.Commit);
        }

        [Test]
        public void ParsesCommitByItself()
        {
            var gref = GithubReferenceParser.Parse("abc1235");
            Assert.Null(gref.User);
            Assert.Null(gref.Repo);
            Assert.AreEqual("abc1235", gref.Commit);
        }

        [Test]
        public void ParsesBranchByItself()
        {
            var gref = GithubReferenceParser.Parse("bug/SC-1234");
            Assert.Null(gref.User);
            Assert.Null(gref.Repo);
            Assert.AreEqual("bug/SC-1234", gref.Branch);
        }

        [Test]
        public void ParsesRepoAndBranch()
        {
            var gref = GithubReferenceParser.Parse("fooRepo@bug/SC-1234");
            Assert.Null(gref.User);
            Assert.AreEqual("fooRepo", gref.Repo);
            Assert.AreEqual("bug/SC-1234", gref.Branch);
        }

        [Test]
        public void ParsesPRByItself()
        {
            var gref = GithubReferenceParser.Parse("#123");
            Assert.Null(gref.User);
            Assert.Null(gref.Repo);
            Assert.AreEqual(123, gref.Issue);
        }

        [Test]
        public void ParsesPRInRepo()
        {
            var gref = GithubReferenceParser.Parse("fooCorp/fooRepo#123");
            Assert.AreEqual("fooCorp", gref.User);
            Assert.AreEqual("fooRepo", gref.Repo);
            Assert.AreEqual(123, gref.Issue);
        }

        [Test]
        public void ParsesExplicitComparison()
        {
            var gref = GithubReferenceParser.Parse("master...bug/SC-1234");
            Assert.Null(gref.User);
            Assert.Null(gref.Repo);
            Assert.AreEqual("master", gref.BaseBranch);
            Assert.AreEqual("bug/SC-1234", gref.Branch);
        }

        [Test]
        public void ParsesExplicitComparisonWithRepo()
        {
            var gref = GithubReferenceParser.Parse("fooCorp/fooRepo@master...bug/SC-1234");
            Assert.AreEqual("fooCorp", gref.User);
            Assert.AreEqual("fooRepo", gref.Repo);
            Assert.AreEqual("master", gref.BaseBranch);
            Assert.AreEqual("bug/SC-1234", gref.Branch);
        }

        [Test]
        public void ReturnsNullForNonGithubReference()
        {
            Assert.Null(GithubReferenceParser.Parse("not a reference"));
        }

        [Test]
        public void ParsesPRInUrl()
        {
            var gref = GithubReferenceParser.Parse("https://github.com/fooCorp/fooRepo/pull/28");
            Assert.AreEqual("fooCorp", gref.User);
            Assert.AreEqual("fooRepo", gref.Repo);
            Assert.AreEqual(28, gref.Issue);
        }

        [Test]
        public void ParsesBranchInUrl()
        {
            var gref = GithubReferenceParser.Parse("https://github.com/fooCorp/fooRepo/tree/labelprinting-extensions");
            Assert.AreEqual("fooCorp", gref.User);
            Assert.AreEqual("fooRepo", gref.Repo);
            Assert.AreEqual("labelprinting-extensions", gref.Branch);
        }

        [Test]
        public void ParsesComparisonInUrl()
        {
            var gref = GithubReferenceParser.Parse("https://github.com/fooCorp/fooRepo/compare/master...bug/SC-1234");
            Assert.AreEqual("fooCorp", gref.User);
            Assert.AreEqual("fooRepo", gref.Repo);
            Assert.AreEqual("master", gref.BaseBranch);
            Assert.AreEqual("bug/SC-1234", gref.Branch);
        }
    }
}
