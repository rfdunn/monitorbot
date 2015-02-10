﻿using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using scbot;

namespace fasttests
{
    class CompositeMessageProcessingTests
    {
        [Test]
        public void CompositeMessageProcessorGroupsTogetherResponses()
        {
            var subProcessor1 = new Mock<IMessageProcessor>();
            var subProcessor2 = new Mock<IMessageProcessor>();

            var response1 = new Response("a", "b");
            subProcessor1.Setup(x => x.ProcessMessage(It.IsAny<Message>())).Returns(new MessageResult(new[] {response1,}));
            var response2 = new Response("c", "d");
            subProcessor2.Setup(x => x.ProcessMessage(It.IsAny<Message>())).Returns(new MessageResult(new[] {response2,}));

            var compositeMessageProcessor = new CompositeMessageProcessor(subProcessor1.Object, subProcessor2.Object);
            var result = compositeMessageProcessor.ProcessMessage(new Message("asdf", "a-user", "some-text"));
            CollectionAssert.AreEqual(new[] {response1, response2}, result.Responses);
        }

        [Test]
        public void ConcattingMessageProcessorConcatsTogetherResponsesToTheSameChannel()
        {
            var subProcessor = new Mock<IMessageProcessor>();

            var response1 = new Response("a", "1");
            var response2 = new Response("c", "2");
            var response3 = new Response("e", "2");
            subProcessor.Setup(x => x.ProcessMessage(It.IsAny<Message>())).Returns(new MessageResult(new[] { response1, response2, response3}));

            var compositeMessageProcessor = new ConcattingMessageProcessor(subProcessor.Object);
            var result = compositeMessageProcessor.ProcessMessage(new Message("asdf", "a-user", "some-text"));
            Assert.AreEqual("a", result.Responses.ElementAt(0).Message);
            Assert.AreEqual("c\ne", result.Responses.ElementAt(1).Message);
        }
    }
}