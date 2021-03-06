// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
#pragma warning disable SA1201 // Elements should appear in the correct order

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AdaptiveExpressions.Properties;
using Microsoft.Bot.Builder.Adapters;
using Microsoft.Bot.Builder.Dialogs.Adaptive;
using Microsoft.Bot.Builder.Dialogs.Adaptive.Actions;
using Microsoft.Bot.Builder.Dialogs.Adaptive.Conditions;
using Microsoft.Bot.Builder.Dialogs.Adaptive.Generators;
using Microsoft.Bot.Builder.Dialogs.Adaptive.Input;
using Microsoft.Bot.Builder.Dialogs.Adaptive.Recognizers;
using Microsoft.Bot.Builder.Dialogs.Adaptive.Templates;
using Microsoft.Bot.Builder.Dialogs.Declarative.Resources;
using Microsoft.Bot.Builder.Dialogs.Memory;
using Microsoft.Bot.Builder.Dialogs.Memory.Scopes;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Microsoft.Bot.Builder.Dialogs.Tests
{
    public class MemoryScopeTests
    {
        private readonly string _testName;

        public MemoryScopeTests(ITestOutputHelper testOutputHelper)
        {
            // Obtains the current running test name.
            var helper = (TestOutputHelper)testOutputHelper;
            var test = (ITest)helper.GetType().GetField("test", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(helper);
            _testName = test.TestCase.TestMethod.Method.Name;
        }

        public TestFlow CreateDialogContext(Func<DialogContext, CancellationToken, Task> handler)
        {
            var adapter = new TestAdapter(TestAdapter.CreateConversation(_testName));
            adapter
                .UseStorage(new MemoryStorage())
                .UseBotState(new UserState(new MemoryStorage()))
                .UseBotState(new ConversationState(new MemoryStorage()));
            var dm = new DialogManager(new LamdaDialog(handler));
            return new TestFlow(adapter, (context, ct) =>
            {
                return dm.OnTurnAsync(context, ct);
            }).SendConversationUpdate();
        }

        [Fact]
        public async Task SimpleMemoryScopesTest()
        {
            await CreateDialogContext(async (dc, ct) =>
            {
                var dsm = dc.State as DialogStateManager;
                foreach (var memoryScope in dsm.Configuration.MemoryScopes.Where(ms => !(ms is ThisMemoryScope ||
                    ms is DialogMemoryScope ||
                    ms is ClassMemoryScope ||
                    ms is DialogClassMemoryScope ||
                    ms is DialogContextMemoryScope)))
                {
                    var memory = memoryScope.GetMemory(dc);
                    Assert.NotNull(memory);
                    ObjectPath.SetPathValue(memory, "test", 15);
                    memory = memoryScope.GetMemory(dc);
                    Assert.Equal(15, ObjectPath.GetPathValue<int>(memory, "test"));
                    ObjectPath.SetPathValue(memory, "test", 25);
                    memory = memoryScope.GetMemory(dc);
                    Assert.Equal(25, ObjectPath.GetPathValue<int>(memory, "test"));
                    memory = memoryScope.GetMemory(dc);
                    ObjectPath.SetPathValue(memory, "source", "destination");
                    ObjectPath.SetPathValue(memory, "{source}", 24);
                    Assert.Equal(24, ObjectPath.GetPathValue<int>(memory, "{source}"));
                    ObjectPath.RemovePathValue(memory, "{source}");
                    Assert.False(ObjectPath.TryGetPathValue<int>(memory, "{source}", out var _));
                    ObjectPath.RemovePathValue(memory, "source");
                    Assert.False(ObjectPath.TryGetPathValue<int>(memory, "{source}", out var _));
                }
            }).StartTestAsync();
        }

        [Fact]
        public async Task BotStateMemoryScopeTest()
        {
            await CreateDialogContext(async (dc, ct) =>
            {
                var dsm = dc.State as DialogStateManager;
                var storage = dc.Context.TurnState.Get<IStorage>();
                var userState = dc.Context.TurnState.Get<UserState>();
                var conversationState = dc.Context.TurnState.Get<ConversationState>();
                var customState = new CustomState(storage);

                dc.Context.TurnState.Add(customState);

                var stateScopes = new (BotState State, MemoryScope Scope)[]
                {
                    (userState, new UserMemoryScope()),
                    (conversationState, new ConversationMemoryScope()),
                    (customState, new BotStateMemoryScope<CustomState>("test")),
                };

                foreach (var stateScope in stateScopes)
                {
                    const string Name = "test-name";
                    const string Value = "test-value";

                    await stateScope.State.CreateProperty<string>(Name).SetAsync(dc.Context, Value, ct);

                    var memory = stateScope.Scope.GetMemory(dc);

                    Assert.Equal(Value, ObjectPath.GetPathValue<string>(memory, Name));
                }
            }).StartTestAsync();
        }

        public class CustomState : BotState
        {
            public CustomState(IStorage storage)
                : base(storage, "Not the name of the type")
            {
            }

            protected override string GetStorageKey(ITurnContext turnContext) => $"botstate/custom/etc";
        }

        public class Foo
        {
            public Foo()
            {
            }

            public StringExpression Title { get; set; }
        }

        public class ComplexDialog : Dialog
        {
            public ComplexDialog()
            {
            }

            public StringExpression String { get; set; }

            public ObjectExpression<Foo> Foo { get; set; }

            public override Task<DialogTurnResult> BeginDialogAsync(DialogContext dc, object options = null, CancellationToken cancellationToken = default)
            {
                dc.Context.SendActivityAsync(dc.State.GetValue<string>("class.id"));
                dc.Context.SendActivityAsync(dc.State.GetValue<string>("dialogclass.id"));
                dc.Context.SendActivityAsync(dc.State.GetValue<string>("class.String"));
                dc.Context.SendActivityAsync(dc.State.GetValue<string>("class.foo.title"));
                return dc.EndDialogAsync();
            }
        }

        [Fact]
        public async Task ClassMemoryScopeTest()
        {
            var adapter = new TestAdapter(TestAdapter.CreateConversation(_testName));
            adapter
                .UseStorage(new MemoryStorage())
                .UseBotState(new UserState(new MemoryStorage()))
                .UseBotState(new ConversationState(new MemoryStorage()));
            var dm = new DialogManager(new AdaptiveDialog("adaptiveDialog")
            {
                Triggers = new List<Adaptive.Conditions.OnCondition>()
                {
                    new OnBeginDialog()
                    {
                        Actions = new List<Dialog>()
                        {
                            new ComplexDialog()
                            {
                                Id = "test",
                                String = "='12345'",
                                Foo = new Foo() { Title = "='abcde'" }
                            }
                        }
                    }
                }
            })
            .UseResourceExplorer(new ResourceExplorer())
            .UseLanguageGeneration();

            await new TestFlow(adapter, (context, ct) =>
            {
                return dm.OnTurnAsync(context, ct);
            })
            .SendConversationUpdate()
                .AssertReply("test")
                .AssertReply("adaptiveDialog")
                .AssertReply("12345")
                .AssertReply("abcde")
            .StartTestAsync();
        }

        [Fact]
        public async Task DialogContextMemoryScopeTest()
        {
            var adapter = new TestAdapter(TestAdapter.CreateConversation(_testName));
            adapter
                .UseStorage(new MemoryStorage())
                .UseBotState(new UserState(new MemoryStorage()))
                .UseBotState(new ConversationState(new MemoryStorage()));
            var dm = new DialogManager(new AdaptiveDialog("adaptiveDialog")
            {
                Triggers = new List<Adaptive.Conditions.OnCondition>()
                {
                    new OnBeginDialog()
                    {
                        Actions = new List<Dialog>()
                        {
                            new AdaptiveDialog("adaptiveDialog2")
                            {
                                Triggers = new List<Adaptive.Conditions.OnCondition>()
                                {
                                    new OnBeginDialog()
                                    {
                                        Actions = new List<Dialog>()
                                        {
                                            new SendActivity(@"${dialogcontext.activeDialog}") { Id = "action1" },
                                            new SendActivity(@"${dialogcontext.parent}"),
                                            new SendActivity(@"${contains(dialogcontext.stack, 'foo')}"),
                                            new SendActivity(@"${contains(dialogcontext.stack, 'adaptiveDialog')}"),
                                            new SendActivity(@"${contains(dialogcontext.stack, 'adaptiveDialog2')}"),
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            })
            .UseResourceExplorer(new ResourceExplorer())
            .UseLanguageGeneration();

            await new TestFlow(adapter, (context, ct) =>
                {
                    return dm.OnTurnAsync(context, ct);
                })
                .SendConversationUpdate()
                    .AssertReply("action1")
                    .AssertReply("adaptiveDialog2")
                    .AssertReply("False")
                    .AssertReply("True")
                    .AssertReply("True")
                .StartTestAsync();
        }

        [Fact]
        public async Task DialogContextMemoryScopeTest_Interruption()
        {
            var adapter = new TestAdapter(TestAdapter.CreateConversation(_testName));
            adapter
                .UseStorage(new MemoryStorage())
                .UseBotState(new UserState(new MemoryStorage()))
                .UseBotState(new ConversationState(new MemoryStorage()));
            var dm = new DialogManager(new AdaptiveDialog("rootDialog")
            {
                AutoEndDialog = false,
                Generator = new TemplateEngineLanguageGenerator(),
                Recognizer = new RegexRecognizer()
                {
                    Intents = new List<IntentPattern>()
                    {
                        new IntentPattern()
                        {
                            Intent = "why",
                            Pattern = "why"
                        }
                    }
                },
                Triggers = new List<OnCondition>()
                {
                    new OnBeginDialog()
                    {
                        Actions = new List<Dialog>()
                        {
                            new SendActivity("Hello"),
                            new TextInput()
                            {
                                Id = "askForName",
                                AlwaysPrompt = true,
                                Prompt = new ActivityTemplate("What is your name?"),
                                Property = "user.name"
                            },
                            new SendActivity("I have ${user.name}")
                        }
                    },
                    new OnIntent()
                    {
                        Intent = "why",
                        Condition = "contains(dialogcontext.stack, 'askForName')",
                        Actions = new List<Dialog>()
                        {
                            new SendActivity("I need your name to complete the sample")
                        }
                    },
                    new OnIntent()
                    {
                        Intent = "why",
                        Actions = new List<Dialog>()
                        {
                            new SendActivity("why what?")
                        }
                    }
                }
            })
            .UseResourceExplorer(new ResourceExplorer())
            .UseLanguageGeneration();

            await new TestFlow(adapter, (context, ct) =>
                {
                    return dm.OnTurnAsync(context, ct);
                })
            .Send("hello")
                .AssertReply("Hello")
                .AssertReply("What is your name?")
            .Send("why")
                .AssertReply("I need your name to complete the sample")
                .AssertReply("What is your name?")
            .Send("tom")
                .AssertReply("I have tom")
            .Send("why")
                .AssertReply("why what?")
            .StartTestAsync();
        }

        internal class BotStateTestDialog : Dialog
        {
            public override async Task<DialogTurnResult> BeginDialogAsync(DialogContext dc, object options = null, CancellationToken cancellationToken = default)
            {
                {
                    var botState = dc.Context.TurnState.Get<ConversationState>();
                    var property = botState.CreateProperty<string>("test");
                    await property.SetAsync(dc.Context, "cool").ConfigureAwait(false);

                    var result = dc.State.GetValue<string>("conversation.test");
                    Assert.Equal("cool", result);
                    dc.State.SetValue("conversation.test", "cool2");
                    Assert.Equal("cool2", await property.GetAsync(dc.Context));
                }

                {
                    var botState = dc.Context.TurnState.Get<UserState>();
                    var property = botState.CreateProperty<string>("test");
                    await property.SetAsync(dc.Context, "cool").ConfigureAwait(false);

                    var result = dc.State.GetValue<string>("user.test");
                    Assert.Equal("cool", result);
                    dc.State.SetValue("user.test", "cool2");
                    Assert.Equal("cool2", await property.GetAsync(dc.Context));
                }

                await dc.Context.SendActivityAsync("next");
                return await dc.EndDialogAsync();
            }
        }

        [Fact]
        public async Task BotStateScopes()
        {
            var storage = new MemoryStorage();
            var adapter = new TestAdapter(TestAdapter.CreateConversation(_testName))
                .UseStorage(storage)
                .UseBotState(new UserState(storage))
                .UseBotState(new ConversationState(storage))
                .Use(new TranscriptLoggerMiddleware(new TraceTranscriptLogger(traceActivity: false)));

            var dm = new DialogManager(new BotStateTestDialog());

            await new TestFlow((TestAdapter)adapter, async (turnContext, cancellationToken) =>
               {
                   await dm.OnTurnAsync(turnContext);
               })
                .Send("hello")
                    .AssertReply("next")
                .StartTestAsync();
        }

        [Fact]
        public async Task DialogMemoryScopeTest()
        {
            var storage = new MemoryStorage();
            var adapter = new TestAdapter(TestAdapter.CreateConversation(_testName))
                .UseStorage(storage)
                .UseBotState(new UserState(storage))
                .UseBotState(new ConversationState(storage))
                .Use(new TranscriptLoggerMiddleware(new TraceTranscriptLogger(traceActivity: false)));

            var dm = new DialogManager(new MemoryScopeTestDialog());

            await new TestFlow((TestAdapter)adapter, async (turnContext, cancellationToken) =>
            {
                await dm.OnTurnAsync(turnContext);
            })
            .Send("hello")
                .AssertReply("next")
            .StartTestAsync();
        }

        [Fact]
        public async Task SettingsMemoryScopeTest()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new List<KeyValuePair<string, string>>() { new KeyValuePair<string, string>("test", "yoyo") })
                .AddJsonFile(@"test.settings.json")
                .Build();

            var storage = new MemoryStorage();
            var adapter = new TestAdapter(TestAdapter.CreateConversation(_testName))
                .Use(new RegisterClassMiddleware<IConfiguration>(configuration))
                .UseStorage(storage)
                .UseBotState(new UserState(storage))
                .UseBotState(new ConversationState(storage))
                .Use(new TranscriptLoggerMiddleware(new TraceTranscriptLogger(traceActivity: false)));

            var dm = new DialogManager(new SettingsScopeTestDialog());

            await new TestFlow((TestAdapter)adapter, async (turnContext, cancellationToken) =>
            {
                await dm.OnTurnAsync(turnContext);
            })
            .Send("settings.test")
                .AssertReply("yoyo")
            .Send("settings.string")
                .AssertReply("test")
            .Send("settings.int")
                .AssertReply("3")
            .Send("settings.array[0]")
                .AssertReply("zero")
            .Send("settings.array[1]")
                .AssertReply("one")
            .Send("settings.array[2]")
                .AssertReply("two")
            .Send("settings.array[3]")
                .AssertReply("three")
            .Send("settings.fakeArray.0")
                .AssertReply("zero")
            .Send("settings.fakeArray.1")
                .AssertReply("one")
            .Send("settings.fakeArray.2")
                .AssertReply("two")
            .Send("settings.fakeArray.3")
                .AssertReply("three")
            .Send("settings.fakeArray.zzz")
                .AssertReply("cat")
            .Send("settings.myArray[0]")
                .AssertReply("e1")
            .Send("settings.myArray[1].obj")
                .AssertReply("e2")
            .Send("settings.nestedArray[0][0]")
                .AssertReply("a")
            .Send("settings.nestedArray[0][1]")
                .AssertReply("b")
            .Send("settings.nestedArray[1][0]")
                .AssertReply("c")
            .Send("settings.nestedArray[1][1]")
                .AssertReply("d")
            .Send("settings.numberArray[0]")
                .AssertReply("0")
            .Send("settings.numberArray[1]")
                .AssertReply("1")
            .Send("settings.sequentialObjectArray[0][0]") // object with number index would be converted into array
                .AssertReply("a")
            .Send("settings.sequentialObjectArray[0][1]")
                .AssertReply("b")
            .Send("settings.sequentialObjectArray[1][0]")
                .AssertReply("c")
            .Send("settings.sequentialObjectArray[1][1]")
                .AssertReply("d")
            .Send("settings.MicrosoftAppPassword") // simple variable
                .AssertReply("null")
            .Send("settings.runtimeSettings.telemetry.options.connectionString") // nested setting
                .AssertReply("null")
            .Send("settings.BlobsStorage.CONNECTIONSTRING") // case in-sensitive 
                .AssertReply("null")
            .Send("settings.BlobsStorage.connectionString")
                .AssertReply("null")
            .StartTestAsync();
        }

        [Fact]
        public async Task TestPathResolvers()
        {
            var storage = new MemoryStorage();
            var adapter = new TestAdapter(TestAdapter.CreateConversation(_testName))
                .UseStorage(storage)
                .UseBotState(new UserState(storage))
                .UseBotState(new ConversationState(storage))
                .Use(new TranscriptLoggerMiddleware(new TraceTranscriptLogger(traceActivity: false)));

            var dm = new DialogManager(new PathResolverTestDialog())
                .UseResourceExplorer(new ResourceExplorer())
                .UseLanguageGeneration();

            await new TestFlow((TestAdapter)adapter, async (turnContext, cancellationToken) =>
            {
                await dm.OnTurnAsync(turnContext);
            })
            .Send("hello")
                .AssertReply("next")
            .StartTestAsync();
        }

        private async Task MissingBotStateScopeTest()
        {
            // test that missing MemoryScope (UserState) behaves like NULL value, aka read ops return null, write ops throw 
            var adapter = new TestAdapter(TestAdapter.CreateConversation(_testName));
            var conversationState = new ConversationState(new MemoryStorage());
            adapter
                .UseStorage(new MemoryStorage())
                .Use(new RegisterClassMiddleware<ConversationState>(conversationState))
                .Use(new AutoSaveStateMiddleware(conversationState));

            var dm = new DialogManager(new LamdaDialog(async (dc, ct) =>
            {
                Assert.Null(dc.State.GetValue<string>("user"));
                Assert.Null(dc.State.GetValue<string>("user.x"));
                try
                {
                    dc.State.SetValue("user.x", "foo");
                    throw new XunitException("Should have throw exception");
                }
                catch (ArgumentException)
                {
                }

                Assert.Null(dc.State.GetValue<string>("user"));
                Assert.Null(dc.State.GetValue<string>("user.x"));
            }));

            await new TestFlow(adapter, (context, ct) =>
            {
                return dm.OnTurnAsync(context, ct);
            })
                .SendConversationUpdate()
            .StartTestAsync();
        }
    }

    internal class MemoryScopeTestDialog : Dialog
    {
        public override async Task<DialogTurnResult> BeginDialogAsync(DialogContext dc, object options = null, CancellationToken cancellationToken = default)
        {
            var dsm = new DialogStateManager(dc);
            foreach (var scope in dsm.Configuration.MemoryScopes.Where(ms => !(ms is DialogMemoryScope) && ms.IncludeInSnapshot == true).Select(ms => ms.Name))
            {
                var path = $"{scope}.test";
                Assert.Null(dc.State.GetValue<string>(path));
                dc.State.SetValue(path, scope);
                Assert.NotNull(dc.State.GetValue<string>(path));
                Assert.Equal(scope, dc.State.GetValue<string>(path));
            }

            await dc.Context.SendActivityAsync("next");
            return await dc.EndDialogAsync();
        }
    }

    internal class SettingsScopeTestDialog : Dialog
    {
        public override async Task<DialogTurnResult> BeginDialogAsync(DialogContext dc, object options = null, CancellationToken cancellationToken = default)
        {
            var value = dc.State.GetValue<string>(dc.Context.Activity.Text) ?? "null";
            await dc.Context.SendActivityAsync(value);
            return await dc.EndDialogAsync();
        }
    }

    internal class PathResolverTestDialog : Dialog
    {
        private readonly string[] entities = new string[] { "test1", "test2" };

        public override async Task<DialogTurnResult> BeginDialogAsync(DialogContext dc, object options = null, CancellationToken cancellationToken = default)
        {
            ValidateSetValue(dc, "#test", "turn.recognized.intents.test");
            ValidateSetValue(dc, "$test", "dialog.test");
            ValidateSetValue(dc, "@@test", "turn.recognized.entities.test", entities);
            Assert.Equal("test1", dc.State.GetValue<string>("@test"));
            Assert.Equal("test2", dc.State.GetValue<string[]>("@@test")[1]);

            ValidateRemoveValue(dc, "#test", "turn.recognized.intents.test");
            ValidateRemoveValue(dc, "$test", "dialog.test");
            ValidateValue(dc, "@test", "turn.recognized.entities.test.first()");
            ValidateRemoveValue(dc, "@@test", "turn.recognized.entities.test");

            await dc.Context.SendActivityAsync("next");
            return await dc.EndDialogAsync();
        }

        private void ValidateSetValue(DialogContext dc, string alias, string path, object value = null)
        {
            Assert.Null(dc.State.GetValue<object>(alias));
            dc.State.SetValue(path, value ?? alias);
            ValidateValue(dc, alias, path);
        }

        private void ValidateValue(DialogContext dc, string alias, string path)
        {
            var p = dc.State.GetValue<object>(path);
            Assert.NotNull(p);
            var a = dc.State.GetValue<object>(alias);
            Assert.NotNull(a);

            Assert.Equal(JsonConvert.SerializeObject(p), JsonConvert.SerializeObject(a));
        }

        private void ValidateRemoveValue(DialogContext dc, string alias, string path)
        {
            ValidateValue(dc, alias, path);
            dc.State.RemoveValue(alias);
            Assert.Null(dc.State.GetValue<object>(path));
            Assert.Null(dc.State.GetValue<object>(alias));
        }
    }
}
