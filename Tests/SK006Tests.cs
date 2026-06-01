using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServiceKit.Analyzers;

namespace ServiceKit.Analyzers.Tests
{
    [TestClass]
    public class SK006Tests
    {
        // Minimal stand-ins for the ServiceKit types the analyzer matches by name.
        private const string ServiceKitStubs = @"
using System;
namespace Nonatomic.ServiceKit
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class ServiceAttribute : Attribute
    {
        public ServiceAttribute(params Type[] serviceTypes) { }
    }

    public abstract class ServiceKitBehaviour { }
}
";

        [TestMethod]
        public async Task AbstractClassWithServiceAttribute_ReportsSK006()
        {
            const string test = ServiceKitStubs + @"
namespace Sample
{
    using Nonatomic.ServiceKit;

    public interface IFoo { }

    [{|SK006:Service(typeof(IFoo))|}]
    public abstract class BaseFoo : ServiceKitBehaviour, IFoo { }
}
";

            await new CSharpAnalyzerTest<ServiceAttributeOnAbstractClassAnalyzer, DefaultVerifier>
            {
                TestCode = test,
            }.RunAsync();
        }

        [TestMethod]
        public async Task ConcreteClassWithServiceAttribute_NoDiagnostic()
        {
            const string test = ServiceKitStubs + @"
namespace Sample
{
    using Nonatomic.ServiceKit;

    public interface IFoo { }

    [Service(typeof(IFoo))]
    public class Foo : ServiceKitBehaviour, IFoo { }
}
";

            await new CSharpAnalyzerTest<ServiceAttributeOnAbstractClassAnalyzer, DefaultVerifier>
            {
                TestCode = test,
            }.RunAsync();
        }

        [TestMethod]
        public async Task AbstractClassWithoutServiceAttribute_NoDiagnostic()
        {
            const string test = ServiceKitStubs + @"
namespace Sample
{
    using Nonatomic.ServiceKit;

    public interface IFoo { }

    public abstract class BaseFoo : ServiceKitBehaviour, IFoo { }
}
";

            await new CSharpAnalyzerTest<ServiceAttributeOnAbstractClassAnalyzer, DefaultVerifier>
            {
                TestCode = test,
            }.RunAsync();
        }

        [TestMethod]
        public async Task CodeFix_RemovesIneffectiveServiceAttribute()
        {
            const string test = ServiceKitStubs + @"
namespace Sample
{
    using Nonatomic.ServiceKit;

    public interface IFoo { }

    [{|SK006:Service(typeof(IFoo))|}]
    public abstract class BaseFoo : ServiceKitBehaviour, IFoo { }
}
";

            // The code fix removes the attribute list with KeepNoTrivia, which also collapses
            // the blank line that preceded the attribute.
            const string fixedCode = ServiceKitStubs + @"
namespace Sample
{
    using Nonatomic.ServiceKit;

    public interface IFoo { }
    public abstract class BaseFoo : ServiceKitBehaviour, IFoo { }
}
";

            await new CSharpCodeFixTest<ServiceAttributeOnAbstractClassAnalyzer, ServiceAttributeOnAbstractClassCodeFix, DefaultVerifier>
            {
                TestCode = test,
                FixedCode = fixedCode,
            }.RunAsync();
        }
    }
}
