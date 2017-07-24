﻿using DSLink.Nodes;
using DSLink.Nodes.Actions;
using DSLink.Request;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DSLink.Example
{
    public class ExampleDSLink : DSLinkContainer
    {
        private readonly Dictionary<string, Value> _rngValues;
        private readonly Random _random;

        public ExampleDSLink(Configuration config) : base(config)
        {
            _rngValues = new Dictionary<string, Value>();
            _random = new Random();

            Responder.AddNodeClass("rng", delegate (Node node)
            {
                node.Configs.Set(ConfigType.Writable, new Value(Permission.Read.Permit));
                node.Configs.Set(ConfigType.ValueType, Nodes.ValueType.Number.TypeValue);
                node.Value.Set(0);

                lock (_rngValues)
                {
                    _rngValues.Add(node.Name, node.Value);
                }
            });

            Task.Run(() => _updateRandomNumbers());
        }

        public override void InitializeDefaultNodes()
        {
            Responder.SuperRoot.CreateChild("createRNG")
                .SetDisplayName("Create RNG")
                .AddParameter(new Parameter
                {
                    Name = "rngName",
                    ValueType = Nodes.ValueType.String
                })
                .SetAction(new ActionHandler(Permission.Config, _createRngAction))
                .BuildNode();
        }

        private async void _updateRandomNumbers()
        {
            lock (_rngValues)
            {
                foreach (var kv in _rngValues)
                {
                    kv.Value.Set(_random.Next());
                }
            }
            await Task.Delay(100);
            _updateRandomNumbers();
        }

        private async void _createRngAction(InvokeRequest request)
        {
            var rngName = request.Parameters["rngName"].Value<string>();
            if (string.IsNullOrEmpty(rngName)) return;
            if (Responder.SuperRoot.Children.ContainsKey(rngName)) return;

            var newRng = Responder.SuperRoot.CreateChild(rngName, "rng")
                .SetConfig(ConfigType.ValueType, new Value(Nodes.ValueType.Number.Type))
                .SetValue(0)
                .BuildNode();

            await request.Close();
            await SaveNodes();
        }
    }
}
