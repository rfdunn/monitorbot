﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using monitorbot.core.utils;
using NUnit.Framework;

namespace monitorbot.core.tests
{
    public class AliasListTests
    {
        [Test]
        public void EmptyAliasListJustPassesThroughStrings()
        {
            var subject = new AliasList();

            Assert.AreEqual("foo", subject.GetCanonicalNameFor("foo"));
            Assert.AreEqual("foo", subject.GetDisplayNameFor("foo"));
        }

        [Test]
        public void CanAddAndFetchDisplayName()
        {
            var subject = new AliasList();
            subject.AddAlias("hiro.protagonist", "Hiro Protagonist", new[] { "The Deliverator", "GreatestSwordsman" });

            Assert.AreEqual("Hiro Protagonist", subject.GetDisplayNameFor("Hiro Protagonist"), 
                "should pass through display name");
            Assert.AreEqual("Hiro Protagonist", subject.GetDisplayNameFor("hiro.protagonist"),
                "should convert canonical name to display name");
            Assert.AreEqual("Hiro Protagonist", subject.GetDisplayNameFor("The Deliverator"),
                "other aliases should also work");
            Assert.AreEqual("Hiro Protagonist", subject.GetDisplayNameFor("GreatestSwordsman"),
                "more than one other aliases should also work");
            Assert.AreEqual("Hiro Protagonist", subject.GetDisplayNameFor("HIRO.PROTAGONIST"),
                "should not be case-sensitive");
            Assert.AreEqual("Hiro Protagonist", subject.GetDisplayNameFor("HIRO PROTAGONIST"), 
                "should canonicalize display name");
        }

        [Test]
        public void CanAddAndFetchCanonicalName()
        {
            var subject = new AliasList();
            subject.AddAlias("hiro.protagonist", "Hiro Protagonist", new[] { "The Deliverator", "GreatestSwordsman" });

            Assert.AreEqual("hiro.protagonist", subject.GetCanonicalNameFor("Hiro Protagonist"),
                "should convert display name to canonical name");
            Assert.AreEqual("hiro.protagonist", subject.GetCanonicalNameFor("hiro.protagonist"),
                "should pass through canonical name");
            Assert.AreEqual("hiro.protagonist", subject.GetCanonicalNameFor("The Deliverator"),
                "other aliases should also work");
            Assert.AreEqual("hiro.protagonist", subject.GetCanonicalNameFor("GreatestSwordsman"),
                "more than one other aliases should also work");
            Assert.AreEqual("hiro.protagonist", subject.GetCanonicalNameFor("HIRO.PROTAGONIST"),
                "should canonicalize canonical name by case");
            Assert.AreEqual("hiro.protagonist", subject.GetCanonicalNameFor("HIRO PROTAGONIST"),
                "should not be case-sensitive");
        }

        [Test]
        public void DoesntCrashOnDuplicates()
        {
            var subject = new AliasList();
            Assert.DoesNotThrow(() =>
            {
                subject.AddAlias("foo", "foo", new[] { "foo", "foo" });
                subject.AddAlias("foo", "foo", new[] { "foo", "foo" });
            });
        }
    }
}
