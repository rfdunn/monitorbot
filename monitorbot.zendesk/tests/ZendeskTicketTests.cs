﻿using System.Linq;
using monitorbot.core.bot;
using monitorbot.zendesk.services;
using Moq;
using NUnit.Framework;

namespace monitorbot.zendesk.tests
{
    class ZendeskTicketTests
    {
        [TestCase("issue ZD#34182")]
        [TestCase("link <https://redgatesupport.zendesk.com/agent/tickets/34182>")]
        public void UsesZendeskApiToPrintBugReferenceDetails(string zendeskReference)
        {
            var api = new Mock<IZendeskTicketApi>(MockBehavior.Strict);
            var ticket = new ZendeskTicket("34182", "SQL Packager 8 crash", "Closed", new ZendeskTicket.Comment[45]);
            api.Setup(x => x.FromId("34182")).ReturnsAsync(ticket);

            var processor = new ZendeskTicketProcessor(api.Object);
            var result = processor.ProcessMessage(new Message("a-channel", "a-user", string.Format("what is {0}", zendeskReference)));
            var response = result.Responses.Single();
            Assert.AreEqual("<https://redgatesupport.zendesk.com/agent/tickets/34182|ZD#34182> | SQL Packager 8 crash | Closed | 45 comments", response.Message);
        }

        [Test]
        public void DoesntMentionTheSameTicketTwice()
        {
            var api = new Mock<IZendeskTicketApi>(MockBehavior.Strict);
            var ticket = new ZendeskTicket("34182", "SQL Packager 8 crash", "Closed", new ZendeskTicket.Comment[45]);
            api.Setup(x => x.FromId("34182")).ReturnsAsync(ticket);

            var processor = new ZendeskTicketProcessor(api.Object);
            var result = processor.ProcessMessage(new Message("a-channel", "a-user", string.Format("what is {0} and {0}", "ZD#34182")));
            var response = result.Responses.Single();
            Assert.AreEqual("<https://redgatesupport.zendesk.com/agent/tickets/34182|ZD#34182> | SQL Packager 8 crash | Closed | 45 comments", response.Message);
        }

        [Test]
        public void IgnoresNullOnError()
        {
            var api = new Mock<IZendeskTicketApi>(MockBehavior.Strict);
            api.Setup(x => x.FromId("12345")).ReturnsAsync(default(ZendeskTicket));

            var processor = new ZendeskTicketProcessor(api.Object);
            var result = processor.ProcessMessage(new Message("a-channel", "a-user", "what is ZD#12345"));
            CollectionAssert.IsEmpty(result.Responses);
        }
    }
}
