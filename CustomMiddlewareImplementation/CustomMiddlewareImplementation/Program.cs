using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace CustomMiddlewareImplementation
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Ready");
            Console.WriteLine("Hello World!");

            var services = new ServiceCollection();
            services.AddTransient<TestService>();

            services.AddRequestHandler<ControlUnitLoaderHandler>();
            services.AddRequestHandler<HandlerB>();
            services.AddRequestHandler<HandlerC>();
            services.AddRequestHandler<HandlerD>();

            var provider = services.BuildServiceProvider();
            var ha2 = provider.GetRequiredService<RequestHandlerProcessor>();

            var context = new RequestContext();

            await ha2.Handle(new TestRequestA(), context);
            await ha2.Handle(new TestRequestB(), context);
            await ha2.Handle(new TestRequestD(), context);

            Console.ReadLine();
        }
    }

    #region Middleware

    public static class AddRequestHandlerExtensions
    {
        private static readonly List<RequestHandlerMetadata> Handlers = new List<RequestHandlerMetadata>();
        private static readonly Dictionary<Type, RequestHandleFactory> RequestHandleFactoriesCache = new Dictionary<Type, RequestHandleFactory>();

        public static IServiceCollection AddRequestHandler<TRequestHandler>(this IServiceCollection services) where TRequestHandler : IRequestHandler
        {
            var handlerType = typeof(TRequestHandler);
            var requestType = GetRequestType(handlerType);
            var requestHandleFactory = GetRequestHandleFactory(requestType);

            var handlerMetadata = new RequestHandlerMetadata
            {
                RequestHandlerType = handlerType,
                RequestHandlerRequestType = requestType
            };

            Handlers.Add(handlerMetadata);
            var index = Handlers.Count - 1;

            services.AddSingleton(handlerType, provider => CreateHandler(provider, handlerMetadata, index, requestHandleFactory));

            if (Handlers.Count == 1)
            {
                services.AddSingleton(provider =>
                {
                    var firstHandler = Handlers[0];
                    provider.GetRequiredService(firstHandler.RequestHandlerType);
                    return new RequestHandlerProcessor(firstHandler.RequestHandle);
                });
            }

            return services;
        }

        private static object CreateHandler(IServiceProvider provider, RequestHandlerMetadata metadata, int index, RequestHandleFactory requestHandleFactory)
        {
            var successorDelegate = GetDelegate(provider, index);

            var handler = ActivatorUtilities.CreateInstance(provider, metadata.RequestHandlerType, successorDelegate);

            metadata.RequestHandle = requestHandleFactory(handler);
            metadata.RequestDelegate = successorDelegate;

            return handler;
        }

        private static Type GetRequestType(Type handlerType)
        {
            return handlerType
                .GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestHandler<>))
                ?.GenericTypeArguments[0];
        }

        private static RequestHandleFactory GetRequestHandleFactory(Type requestType)
        {
            if (RequestHandleFactoriesCache.TryGetValue(requestType, out var factory))
                return factory;

            var requestHandleFactoryType = typeof(RequestHandleFactory<>).MakeGenericType(requestType);
            var requestHandleFactory = Activator.CreateInstance(requestHandleFactoryType);
            factory = handler => (RequestDelegate)requestHandleFactoryType.GetMethod("CreateRequestDelegate")
                ?.Invoke(requestHandleFactory, new[] { handler });

            RequestHandleFactoriesCache.Add(requestType, factory);

            return factory;
        }

        private static RequestDelegate GetDelegate(IServiceProvider provider, int index)
        {
            if (index == Handlers.Count - 1)
                return RequestDelegateFactory.EmptyHandlerAction;

            var nextHandlerMetadata = Handlers[index + 1];

            provider.GetRequiredService(nextHandlerMetadata.RequestHandlerType);

            return RequestDelegateFactory.CreateRequestDelegate(nextHandlerMetadata.RequestHandlerRequestType,
                nextHandlerMetadata.RequestHandle, nextHandlerMetadata.RequestDelegate);
        }

        private class RequestHandlerMetadata
        {
            public Type RequestHandlerType { get; set; }

            public Type RequestHandlerRequestType { get; set; }

            public RequestDelegate RequestHandle { get; set; }

            public RequestDelegate RequestDelegate { get; set; }
        }

        private class RequestHandleFactory<TRequest> where TRequest : IRequest
        {
            // ReSharper disable once UnusedMember.Local
            public RequestDelegate CreateRequestDelegate(IRequestHandler<TRequest> handler)
            {
                return (req, ctx) => handler.Handle((TRequest)req, ctx);
            }
        }

        private delegate RequestDelegate RequestHandleFactory(object obj);
    }

    public class RequestHandlerProcessor
    {
        private readonly RequestDelegate _initialDelegate;

        public RequestHandlerProcessor(RequestDelegate initialDelegate)
        {
            _initialDelegate = initialDelegate;
        }

        public Task Handle(IRequest request, RequestContext context)
        {
            return _initialDelegate(request, context);
        }
    }

    public interface IRequestHandler
    {
    }

    public interface IRequestHandler<in TRequest> : IRequestHandler where TRequest : IRequest
    {
        Task Handle(TRequest request, RequestContext context);
    }

    public static class RequestDelegateFactory
    {
        public static RequestDelegate EmptyHandlerAction = (req, ctx) => Task.CompletedTask;

        public static RequestDelegate CreateRequestDelegate<TRequest>(IRequestHandler<TRequest> successor, RequestDelegate successorRequestDelegate) where TRequest : IRequest
        {
            var handlerRequestType = typeof(TRequest);

            Task RequestDelegate(IRequest request, RequestContext context) =>
                handlerRequestType.IsInstanceOfType(request) ? successor.Handle((TRequest)request, context) : successorRequestDelegate(request, context);

            return RequestDelegate;
        }

        public static RequestDelegate CreateRequestDelegate(Type successorRequestType, RequestDelegate successorHandle, RequestDelegate successorRequestDelegate)
        {
            return (req, ctx) => successorRequestType.IsInstanceOfType(req) ? successorHandle(req, ctx) : successorRequestDelegate(req, ctx);
        }
    }

    public interface IRequest
    {
    }

    public class RequestContext
    {
    }

    public delegate Task RequestDelegate(IRequest request, RequestContext context);

    #endregion

    public interface IMediaRequest : IRequest
    {
    }

    public class TestRequestA : IRequest
    {
    }

    public class TestRequestB : IMediaRequest
    {
    }

    public class TestRequestD : IRequest
    {
    }

    public class ControlUnitLoaderHandler : IRequestHandler<IRequest>
    {
        private readonly RequestDelegate _next;

        public ControlUnitLoaderHandler(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Handle(IRequest request, RequestContext context)
        {
            Console.WriteLine("A Started");
            await _next(request, context);
            Console.WriteLine("A Completed");
        }
    }

    public class HandlerB : IRequestHandler<IMediaRequest>
    {
        private readonly RequestDelegate _next;
        private readonly TestService _service;

        public HandlerB(TestService service, RequestDelegate next)
        {
            _next = next;
            _service = service;
        }

        public async Task Handle(IMediaRequest request, RequestContext context)
        {
            Console.WriteLine("B Started");
            await _next(request, context);
            Console.WriteLine("B Completed");
        }
    }

    public class HandlerC : IRequestHandler<IRequest>
    {
        private readonly RequestDelegate _next;

        public HandlerC(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Handle(IRequest request, RequestContext context)
        {
            Console.WriteLine("C Started");
            await _next(request, context);
            Console.WriteLine("C Completed");
        }
    }

    public class HandlerD : IRequestHandler<TestRequestD>
    {
        private readonly RequestDelegate _next;

        public HandlerD(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Handle(TestRequestD request, RequestContext context)
        {
            Console.WriteLine("D Started");
            await _next(request, context);
            Console.WriteLine("D Completed");
        }
    }

    public class TestService
    {
        public String Type { get; set; }
    }
}
