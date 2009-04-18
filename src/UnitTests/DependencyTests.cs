﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Hiro.UnitTests.SampleDomain;
using System.Reflection;
using Moq;

namespace Hiro.UnitTests
{
    [TestFixture]
    public class DependencyTests : BaseFixture
    {
        [Test]
        public void ShouldBeEqualIfServiceNameAndServiceTypeAreTheSame()
        {
            var first = new Dependency(string.Empty, typeof(IPerson));
            var second = new Dependency(string.Empty, typeof(IPerson));

            Assert.AreEqual(first, second);
            Assert.AreEqual(first.GetHashCode(), second.GetHashCode());
        }

        [Test]
        public void ShouldBeAbleToAddItemsToDependencyMap()
        {
            var ctor = typeof(Vehicle).GetConstructor(new Type[0]);
            var resolver = new Mock<IDependencyResolver<ConstructorInfo>>();
            var dependency = new Dependency(string.Empty, typeof(IVehicle));
            var constructorImplementation = new Implementation<ConstructorInfo>(ctor, resolver.Object);

            var dependencyMap = new DependencyMap();
            dependencyMap.AddImplementation(dependency, constructorImplementation);
            Assert.IsTrue(dependencyMap.Contains(dependency));
        }

        [Test]
        public void ShouldReturnImplementationsFromDependencyMapFromImplementationsThatHaveNoMissingDependencies()
        {
            var map = new DependencyMap();            
            var dependency = new Dependency(string.Empty, typeof(IVehicle));
            var implementation = new Mock<IImplementation>();
            implementation.Expect(impl => impl.GetMissingDependencies(map)).Returns(new IDependency[0]);
            
            bool addIncompleteImplementations = false;
            map.AddImplementation(dependency, implementation.Object);
            var results = map.GetImplementations(dependency, addIncompleteImplementations);

            Assert.IsTrue(results.Count() > 0);
            Assert.IsTrue(results.Contains(implementation.Object));

            implementation.VerifyAll();
        }
    }
}