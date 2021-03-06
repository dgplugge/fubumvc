using System;
using System.Collections.Generic;
using System.Linq;
using FubuCore;
using FubuMVC.Core;
using FubuMVC.Core.Assets;
using FubuMVC.Core.Behaviors;
using FubuMVC.Core.Caching;
using FubuMVC.Core.Http;
using FubuMVC.Core.Registration;
using FubuMVC.Core.Registration.Nodes;
using FubuMVC.Core.Resources.Conneg;
using FubuMVC.Core.Runtime;
using FubuMVC.StructureMap;
using FubuMVC.Tests.Urls;
using FubuTestingSupport;
using NUnit.Framework;
using Rhino.Mocks;
using StructureMap;

namespace FubuMVC.Tests.Registration.Expressions
{
    [TestFixture]
    public class WrapBehaviorChainsWithTester
    {
        #region Setup/Teardown

        [SetUp]
        public void SetUp()
        {
            AssetContentEndpoint.Latched = true;

            registry = new FubuRegistry(x =>
            {
                x.Actions.IncludeTypes(t => false);

                // Tell FubuMVC to wrap the behavior chain for each
                // RouteHandler with the "FakeUnitOfWorkBehavior"
                // Kind of like a global [ActionFilter] in MVC
                x.Policies.WrapBehaviorChainsWith<FakeUnitOfWorkBehavior>();

                // Explicit junk you would only do for exception cases to
                // override the conventions
                x.Route("area/sub/{Name}/{Age}")
                    .Calls<TestController>(c => c.AnotherAction(null)).OutputToJson();

                x.Route("area/sub2/{Name}/{Age}")
                    .Calls<TestController>(c => c.AnotherAction(null)).OutputToJson();

                x.Route("area/sub3/{Name}/{Age}")
                    .Calls<TestController>(c => c.AnotherAction(null)).OutputToJson();
            });
        }

        #endregion

        [TearDown]
        public void TearDown()
        {
            AssetContentEndpoint.Latched = false;
        }

        private FubuRegistry registry;

        public class FakeUnitOfWorkBehavior : IActionBehavior
        {
            private readonly IActionBehavior _inner;

            public FakeUnitOfWorkBehavior(IActionBehavior inner)
            {
                _inner = inner;
            }

            public IActionBehavior Inner
            {
                get { return _inner; }
            }

            public void Invoke()
            {
            }

            public void InvokePartial()
            {
                throw new NotImplementedException();
            }
        }

        [Test]
        public void all_behaviors_chains_should_start_with_the_declared_behavior()
        {
            var graph = BehaviorGraph.BuildFrom(registry);

            graph.Behaviors.Count().ShouldEqual(3);
            var visitor = new BehaviorVisitor(new NulloConfigurationObserver(), "");
            visitor.Actions += chain =>
            {
                // Don't bother with the asset node
                if (chain.Any(x => x is OutputCachingNode)) return;

                // Input node is first
                var wrapper = chain.Top.Next.ShouldBeOfType<Wrapper>();
                wrapper.BehaviorType.ShouldEqual(typeof (FakeUnitOfWorkBehavior));
                wrapper.Next.ShouldBeOfType<ActionCall>();
            };

            graph.VisitBehaviors(visitor);
        }

        [Test]
        public void hydrate_through_container_facility_smoke_test()
        {
            var container = new Container(x =>
            {
                x.For<IStreamingData>().Use(MockRepository.GenerateMock<IStreamingData>());
                x.For<IHttpWriter>().Use(new NulloHttpWriter());
                x.For<ICurrentChain>().Use(new CurrentChain(null, null));
                x.For<ICurrentHttpRequest>().Use(new StubCurrentHttpRequest{
                    TheApplicationRoot = "http://server"
                });

                x.For<IResourceHash>().Use(MockRepository.GenerateMock<IResourceHash>());
            });

            FubuApplication.For(() => registry).StructureMap(container).Bootstrap();

            container.Model.InstancesOf<IActionBehavior>().Count().ShouldBeGreaterThan(3);

            // The InputBehavior is first
            container.GetAllInstances<IActionBehavior>().Each(x =>
            {
                // Don't mess with the asset content chain
                if (x is OutputCachingBehavior) return;

                if (x.GetType().Closes(typeof (InputBehavior<>)))
                {
                    x.As<BasicBehavior>().InsideBehavior.ShouldBeOfType<FakeUnitOfWorkBehavior>().Inner.ShouldNotBeNull();
                }
                else
                {
                    x.ShouldBeOfType<FakeUnitOfWorkBehavior>().Inner.ShouldNotBeNull();
                }
            });
        }
    }
}