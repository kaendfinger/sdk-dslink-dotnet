using System.Collections.Generic;
using System.Threading;
using DSLink;
using DSLink.NET;
using DSLink.Util.Logger;
using DSLink.Respond;
using DSLink.Nodes;
using DSLink.Nodes.Actions;
using System.Threading.Tasks;

namespace RNG
{
    public class ExampleDSLink : DSLinkContainer
    {
        public ExampleDSLink(Configuration config) : base(config)
        {
            var node = Responder.SuperRoot.CreateChild("Test")
                                .AddParameter(new Parameter("string", "string"))
                                .AddParameter(new Parameter("int", "int"))
                                .AddParameter(new Parameter("number", "number"))
                                .AddColumn(new Column("success", "bool"))
                                .SetAction(new ActionHandler(Permission.Write, async (request) =>
                                {
                                    await request.UpdateTable(new Table
                                    {
                                        new Row
                                        {
                                            true
                                        }
                                    });
                                    await request.Close();
                                }))
                                .BuildNode();
            
            Responder.SuperRoot.CreateChild("TestVal")
                               .SetWritable(Permission.Read)
                               .SetType(ValueType.Number)
                               .SetValue(0.1)
                               .BuildNode();

            Task.Run(async () =>
            {
                await Task.Delay(5000);
                int num = 0;
                Value value = Responder.SuperRoot.Get("/TestVal").Value;

                while (true)
                {
                    value.Set(num++);
                }
            });
        }

        private static void Main(string[] args)
        {
            Initialize();

            while (true)
            {
                Thread.Sleep(1000);
            }
        }

        public static async void Initialize()
        {
            NETPlatform.Initialize();
            var dslink =
                new ExampleDSLink(new Configuration(new List<string>(), "sdk-dotnet",
                                                    responder: true, requester: true,
                                                    communicationFormat: "msgpack"));

            dslink.Connect();
        }
    }
}
