using System;
using System.Collections.Generic;
using System.Reactive.Threading.Tasks;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Funq;
using Microsoft.Reactive.Testing;
using ReactiveUI;
using ReactiveUI.Testing;
using ServiceStack;
using Splat;

namespace Test.Rx.ServiceStack
{
    public class AppHost : AppHostHttpListenerBase
    {
        public AppHost(string serviceName, params Assembly[] assembliesWithServices)
            : base(serviceName, assembliesWithServices)
        {
        }

        public AppHost(string serviceName, string handlerPath, params Assembly[] assembliesWithServices)
            : base(serviceName, handlerPath, assembliesWithServices)
        {
        }

        public override void Configure(Container container)
        {
            container.Register(c => new TestService());
        }
    }

    public class TestRequest : IReturn<IList<string>>
    {
    }

    public class TestService : Service
    {
        public object Get(TestRequest request)
        {
            return new List<string>
            {
                "1",
                "2"
            };
        }
    }

    public interface ITestViewModel : IRoutableViewModel
    {
        IReactiveCommand ServiceCommand { get; }
    }

    public class TestViewModel : ReactiveObject, ITestViewModel
    {
        private ReactiveList<string> _reactiveList = new ReactiveList<string>();

        public TestViewModel(IScreen screen = null)
        {
            HostScreen = screen;

            ServiceReadCommand = ReactiveCommand.CreateAsyncObservable(x => ServiceCommandTask(), RxApp.TaskpoolScheduler);
            ServiceReadCommand.ThrownExceptions.Subscribe(x => Console.WriteLine((object)x));
            ServiceReadCommand.Subscribe(x =>
            {
                foreach (var item in x)
                {
                    _reactiveList.Add(item);
                }
            });
        }

        private IObservable<IList<string>> ServiceCommandTask()
        {
            var baseUri = "http://localhost:9010";
            var client = new JsonServiceClient(baseUri);
            return (client.GetAsync(new TestRequest())).ToObservable();
            //var res = client.Get(new TestRequest());
            //return Observable.Return(new List<string> { "" });
            //return await client.GetAsync(new TestRequest());
        }

        public IScreen HostScreen { get; protected set; }

        IReactiveCommand ITestViewModel.ServiceCommand { get { return ServiceReadCommand; } }

        public ReactiveCommand<IList<string>> ServiceReadCommand { get; protected set; }

        public ReactiveList<string> ReactiveList
        {
            get { return _reactiveList; }
            set { _reactiveList = this.RaiseAndSetIfChanged(ref _reactiveList, value); }
        }

        public string UrlPathSegment
        {
            get { return "Test"; }
        }
    }

    internal class Program
    {
        private static void Main(string[] args)
        {
            Console.WriteLine("Start");
            var appHost = new AppHost("Test.Server", typeof(Program).Assembly);

            appHost.Init();
            appHost.Start("http://*:9010/");

            //var async = GetAsync();
            //Console.WriteLine("Async=" + async != null);

            //var noasync = Get();
            //Console.WriteLine("Noasync=" + noasync != null);

            IList<string> res = null;
            var rl = new TestScheduler().With(sched =>
            {
                var viewModel = new TestViewModel();

                viewModel.ServiceReadCommand.CanExecute(null).Should().BeTrue();
                viewModel.ServiceReadCommand.ExecuteAsync(null).Subscribe(x => res = x);

                //Thread.Sleep(1000);
                sched.AdvanceByMs(1000);

                return viewModel.ReactiveList;
            });

            Console.WriteLine("Result=" + (res != null));

            Console.WriteLine("Press any key...");
            Console.ReadKey();
        }

        private static IList<string> Get()
        {
            var baseUri = "http://localhost:9010";
            var client = new JsonServiceClient(baseUri);

            return client.Get(new TestRequest());
        }

        private static async Task<IList<string>> GetAsync()
        {
            var baseUri = "http://localhost:9010";
            var client = new JsonServiceClient(baseUri);

            return await client.GetAsync(new TestRequest());
        }
    }
}