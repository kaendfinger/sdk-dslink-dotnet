﻿using DSLink.Connection;
using DSLink.Platform;
using DSLink.Respond;
using Moq;
using NUnit.Framework;
using System.Collections.Generic;
using PCLStorage;
using Newtonsoft.Json.Linq;
using DSLink.Nodes;
using System.Threading.Tasks;
using System;
using DSLink.Nodes.Actions;

namespace DSLink.Tests
{
    [TestFixture]
    public class ResponderTests
    {
        private Configuration _config;
        private DSLinkResponder _responder;
        private Mock<IFolder> _mockFolder;
        private Mock<DSLinkContainer> _mockContainer;
        private Mock<Connector> _mockConnector;

        [SetUp]
        public void SetUp()
        {
            _mockFolder = new Mock<IFolder>();

            BasePlatform.SetPlatform(new TestPlatform(_mockFolder));
            _config = new Configuration(new List<string>(), "Test", responder: true);

            _mockContainer = new Mock<DSLinkContainer>();
            _mockConnector = new Mock<Connector>(_mockContainer.Object);

            _mockContainer.SetupGet(c => c.Config).Returns(_config);
            _mockContainer.SetupGet(c => c.Connector).Returns(_mockConnector.Object);

            _responder = new DSLinkResponder(_mockContainer.Object);

            _mockContainer.SetupGet(c => c.Responder).Returns(_responder);

            _responder.SuperRoot.CreateChild("testValue")
                .SetType(Nodes.ValueType.Number)
                .SetValue(123)
                .BuildNode();

            _responder.SuperRoot.CreateChild("testNodeConfigs")
                .SetConfig("testString", new Value("string"))
                .SetConfig("testNumber", new Value(123))
                .BuildNode();
        }

        private Task<JArray> _listNode()
        {
            return _responder.ProcessRequests(new JArray
            {
                new JObject
                {
                    new JProperty("rid", 1),
                    new JProperty("method", "list"),
                    new JProperty("path", "/")
                }
            });
        }

        private Task<JArray> _invokeNode()
        {
            return _responder.ProcessRequests(new JArray
            {
                new JObject
                {
                    new JProperty("rid", 1),
                    new JProperty("method", "invoke"),
                    new JProperty("permit", "write"),
                    new JProperty("path", "/testAction"),
                    new JProperty("params", new JObject
                    {
                        new JProperty("testString", "string"),
                        new JProperty("testNumber", 123)
                    })
                }
            });
        }

        private Task<JArray> _subscribeToNode()
        {
            return _responder.ProcessRequests(new JArray
            {
                new JObject
                {
                    new JProperty("rid", 1),
                    new JProperty("method", "subscribe"),
                    new JProperty("paths", new JArray
                    {
                        new JObject
                        {
                            new JProperty("path", "/testValue"),
                            new JProperty("sid", 0)
                        }
                    })
                }
            });
        }

        private Task<JArray> _unsubscribeFromNode()
        {
            return _responder.ProcessRequests(new JArray
            {
                new JObject
                {
                    new JProperty("rid", 2),
                    new JProperty("method", "unsubscribe"),
                    new JProperty("sids", new JArray
                    {
                        0
                    })
                }
            });
        }

        [Test]
        public async Task TestList()
        {
            var responses = await _listNode();
            var response = responses[0];
            var updates = response["updates"];

            Assert.AreEqual(1, response["rid"].Value<int>());
            Assert.AreEqual("open", response["stream"].Value<string>());

            Assert.IsTrue(JToken.DeepEquals(updates[0], new JArray
            {
                "$is",
                "node"
            }));

            var testValueUpdate = updates[1][1];
            Assert.AreEqual("testValue", updates[1][0].Value<string>());
            Assert.NotNull(testValueUpdate["$is"]);
            Assert.NotNull(testValueUpdate["$type"]);
            Assert.NotNull(testValueUpdate["value"]);
            Assert.NotNull(testValueUpdate["ts"]);
            Assert.AreEqual("node", testValueUpdate["$is"].Value<string>());
            Assert.AreEqual("number", testValueUpdate["$type"].Value<string>());
            Assert.AreEqual(123, testValueUpdate["value"].Value<int>());
            Assert.AreEqual(JTokenType.String, testValueUpdate["ts"].Type);

            var testNodeUpdate = updates[2][1];
            Assert.AreEqual("testNodeConfigs", updates[2][0].Value<string>());
            Assert.NotNull(testNodeUpdate["$is"]);
            Assert.NotNull(testNodeUpdate["$testString"]);
            Assert.NotNull(testNodeUpdate["$testNumber"]);
            Assert.AreEqual("node", testNodeUpdate["$is"].Value<string>());
            Assert.AreEqual("string", testNodeUpdate["$testString"].Value<string>());
            Assert.AreEqual(123, testNodeUpdate["$testNumber"].Value<int>());

            Console.WriteLine(updates.ToString());
        }

        [Test]
        public async Task TestInvoke()
        {
            bool actionInvoked = false;
            _responder.SuperRoot.CreateChild("testAction")
                .SetInvokable(Permission.Write)
                .SetAction(new ActionHandler(Permission.Write, async (request) =>
                {
                    actionInvoked = true;
                    await request.Close();
                }))
                .BuildNode();

            await _invokeNode();

            Assert.IsTrue(actionInvoked);
        }

        [Test]
        public async Task TestInvokeParameters()
        {
            _responder.SuperRoot.CreateChild("testAction")
                .SetInvokable(Permission.Write)
                .SetAction(new ActionHandler(Permission.Write, async (request) =>
                {
                    Assert.AreEqual("string", request.Parameters["testString"].Value<string>());
                    Assert.AreEqual(123, request.Parameters["testNumber"].Value<int>());
                    await request.Close();
                }))
                .BuildNode();

            await _invokeNode();
        }

        [Test]
        public async Task TestSubscribe()
        {
            var requestResponses = await _subscribeToNode();

            var requestUpdate = requestResponses[0];
            var update = requestUpdate["updates"][0];
            var requestClose = requestResponses[1];

            // Test for subscribe method value update.
            Assert.AreEqual(0, requestUpdate["rid"].Value<int>()); // Request ID
            Assert.AreEqual(0, update[0].Value<int>()); // Subscription ID
            Assert.AreEqual(123, update[1].Value<int>()); // Value
            Assert.IsInstanceOf(typeof(string), update[2].Value<string>()); // Timestamp TODO: Test if valid

            // Test for subscribe method stream close.
            Assert.AreEqual(1, requestClose["rid"].Value<int>());
            Assert.AreEqual("closed", requestClose["stream"].Value<string>());
        }

        [Test]
        public async Task TestUnsubscribe()
        {
            await _subscribeToNode();
            var requestResponses = await _unsubscribeFromNode();

            var requestClose = requestResponses[0];

            // Test for unsubscribe method stream close.
            Assert.AreEqual(2, requestClose["rid"].Value<int>());
            Assert.AreEqual("closed", requestClose["stream"].Value<string>());
        }
    }
}
