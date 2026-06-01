using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServiceKit.Analyzers;

namespace ServiceKit.Analyzers.Tests
{
    [TestClass]
    public class SK004Tests
    {
        private const string Stubs = @"
using System;
using System.Threading;
using System.Threading.Tasks;
namespace Nonatomic.ServiceKit
{
    public interface IServiceInjectionBuilder
    {
        IServiceInjectionBuilder WithCancellation(CancellationToken token);
        Task ExecuteAsync();
        Task ExecuteWithCancellationAsync(CancellationToken token);
    }
    public interface IServiceKitLocator
    {
        IServiceInjectionBuilder Inject(object target);
        IServiceInjectionBuilder InjectServicesAsync(object target);
    }
}
";

        private static string Consumer(string body) => Stubs + @"
namespace Sample
{
    using System.Threading;
    using System.Threading.Tasks;
    using Nonatomic.ServiceKit;

    class Consumer
    {
        IServiceKitLocator _locator;
        CancellationToken destroyCancellationToken;
        async Task M()
        {
            " + body + @"
        }
    }
}
";

        private static Task Verify(string body) =>
            new CSharpAnalyzerTest<InjectionMustIncludeCancellationAnalyzer, DefaultVerifier>
            {
                TestCode = Consumer(body),
            }.RunAsync();

        [TestMethod]
        public Task Inject_WithoutCancellation_ReportsSK004() =>
            Verify(@"await _locator.Inject(this).{|SK004:ExecuteAsync|}();");

        [TestMethod]
        public Task Inject_WithCancellation_NoDiagnostic() =>
            Verify(@"await _locator.Inject(this).WithCancellation(destroyCancellationToken).ExecuteAsync();");

        [TestMethod]
        public Task Inject_ExecuteWithCancellationAsync_NoDiagnostic() =>
            Verify(@"await _locator.Inject(this).ExecuteWithCancellationAsync(destroyCancellationToken);");

        [TestMethod]
        public Task InjectServicesAsync_WithoutCancellation_StillReportsSK004() =>
            Verify(@"await _locator.InjectServicesAsync(this).{|SK004:ExecuteAsync|}();");
    }
}
