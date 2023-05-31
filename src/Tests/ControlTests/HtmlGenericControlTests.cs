using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Threading.Tasks;
using CheckTestOutput;
using DotVVM.Framework.Compilation;
using DotVVM.Framework.Controls;
using DotVVM.Framework.Tests.Binding;
using DotVVM.Framework.ViewModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DotVVM.Framework.Testing;
using System.Security.Claims;
using System.Collections;
using DotVVM.Framework.Binding;
using DotVVM.Framework.Compilation.ControlTree;
using DotVVM.Framework.Hosting;

namespace DotVVM.Framework.Tests.ControlTests
{
    [TestClass]
    public class HtmlGenericControlTests
    {
        static readonly ControlTestHelper cth = new ControlTestHelper(config: config => {
        });
        static readonly BindingCompilationService bindingService = cth.GetService<BindingCompilationService>();
        static readonly IDotvvmRequestContext defaultContext = DotvvmTestHelper.CreateContext(cth.Configuration);
        static readonly DataContextStack defaultDataContext = DataContextStack.Create(typeof(BasicTestViewModel), extensionParameters: new [] { new JsExtensionParameter("p1", false) });
        readonly OutputChecker check = new OutputChecker("testoutputs");

        string RenderToString(DotvvmControl control, object viewModel = null, IDotvvmRequestContext context = null)
        {
            context ??= defaultContext;
            viewModel ??= new BasicTestViewModel();

            control.SetValue(Internal.RequestContextProperty, context);
            control.DataContext = viewModel;
            using  (var sw = new System.IO.StringWriter())
            {
                var html = new HtmlWriter(sw, context);
                control.Render(html, context);
                return sw.ToString();
            }
        }

        [TestMethod]
        public void ClientSideInvoke_Attribute()
        {
            var control = new HtmlGenericControl("div")
                .SetProperty(RenderSettings.ModeProperty, RenderMode.Client)
                .SetAttribute("data-test", bindingService.Cache.CreateValueBinding<string>("_js.Invoke<string>('testMethod')", defaultDataContext));

            var str = RenderToString(control);
            Assert.AreEqual("""<div data-bind='attr: { "data-test": dotvvm.viewModules.call("p1", "testMethod", [], false) }'></div>""", str);
        }
        [TestMethod]
        public void ClientSideInvoke_Visible()
        {
            var control = new HtmlGenericControl("div")
                .SetProperty(RenderSettings.ModeProperty, RenderMode.Client)
                .SetProperty(c => c.Visible, bindingService.Cache.CreateValueBinding<bool>("_js.Invoke<bool>('testMethod')", defaultDataContext));

            var str = RenderToString(control);
            Assert.AreEqual("""<div data-bind='visible: dotvvm.viewModules.call("p1", "testMethod", [], false)'></div>""", str);
        }
        [TestMethod]
        public void ClientSideInvoke_Class()
        {
            var control = new HtmlGenericControl("div")
                .SetProperty(RenderSettings.ModeProperty, RenderMode.Client)
                .AddCssClass("test-class", bindingService.Cache.CreateValueBinding<bool>("_js.Invoke<bool>('testMethod')", defaultDataContext));

            var str = RenderToString(control);
            Assert.AreEqual("""<div data-bind='css: { "test-class": dotvvm.viewModules.call("p1", "testMethod", [], false) }'></div>""", str);
        }
        [TestMethod]
        public void ClientSideInvoke_Style()
        {
            var control = new HtmlGenericControl("div")
                .SetProperty(RenderSettings.ModeProperty, RenderMode.Client)
                .AddCssStyle("width", bindingService.Cache.CreateValueBinding<int>("_js.Invoke<int>('testMethod')", defaultDataContext));

            var str = RenderToString(control);
            Assert.AreEqual("""<div data-bind='style: { width: dotvvm.viewModules.call("p1", "testMethod", [], false) }'></div>""", str);
        }

        [TestMethod]
        public void ServerSideInvokeFails_Attribute()
        {
            var control = new HtmlGenericControl("div")
                .SetProperty(RenderSettings.ModeProperty, RenderMode.Server)
                .SetAttribute("data-test", bindingService.Cache.CreateValueBinding<string>("_js.Invoke<string>('testMethod')", defaultDataContext));

            var exception = Assert.ThrowsException<DotvvmControlException>(() => RenderToString(control));
        }

        [TestMethod]
        public void ServerSideInvokeFails_Visible()
        {
            var control = new HtmlGenericControl("div")
                .SetProperty(RenderSettings.ModeProperty, RenderMode.Server)
                .SetProperty(c => c.Visible, bindingService.Cache.CreateValueBinding<bool>("_js.Invoke<bool>('testMethod')", defaultDataContext));

            var exception = Assert.ThrowsException<DotvvmControlException>(() => RenderToString(control));
        }
        [TestMethod]
        [Ignore] // TODO?
        public void ServerSideInvokeFails_Class()
        {
            var control = new HtmlGenericControl("div")
                .SetProperty(RenderSettings.ModeProperty, RenderMode.Server)
                .AddCssClass("test-class", bindingService.Cache.CreateValueBinding<bool>("_js.Invoke<bool>('testMethod')", defaultDataContext));

            var exception = Assert.ThrowsException<Exception>(() => RenderToString(control));
        }
        [TestMethod]
        [Ignore] // TODO?
        public void ServerSideInvokeFails_Style()
        {
            var control = new HtmlGenericControl("div")
                .SetProperty(RenderSettings.ModeProperty, RenderMode.Server)
                .AddCssStyle("width", bindingService.Cache.CreateValueBinding<int>("_js.Invoke<int>('testMethod')", defaultDataContext));

            var exception = Assert.ThrowsException<Exception>(() => RenderToString(control));
        }

        [DataTestMethod]
        [DataRow(RenderMode.Client)]
        [DataRow(RenderMode.Server)]
        public void ValueBinding_Attribute(RenderMode renderMode)
        {
            var control = new HtmlGenericControl("div")
                .SetProperty(RenderSettings.ModeProperty, renderMode)
                .SetAttribute("data-test", bindingService.Cache.CreateValueBinding<string>("String", defaultDataContext));

            var str = RenderToString(control);
            // TODO: should it behave differently for server-side rendering?
            Assert.AreEqual($$"""<div {{(renderMode == RenderMode.Server ? "data-test=some-string " : "")}}data-bind='attr: { "data-test": String }'></div>""", str);
        }

        [DataTestMethod]
        [DataRow(RenderMode.Client)]
        [DataRow(RenderMode.Server)]
        public void ValueBinding_Visible(RenderMode renderMode) // RenderMode should not matter
        {
            var control = new HtmlGenericControl("div")
                .SetProperty(RenderSettings.ModeProperty, renderMode)
                .SetProperty(c => c.Visible, bindingService.Cache.CreateValueBinding<bool>("Integer == 99", defaultDataContext));

            var str = RenderToString(control);
            Assert.AreEqual("""<div style=display:none data-bind="visible: Integer() == 99"></div>""", str);
        }

        [DataTestMethod]
        [DataRow(RenderMode.Client)]
        [DataRow(RenderMode.Server)]
        public void ValueBinding_Class(RenderMode renderMode) // RenderMode should not matter
        {
            var control = new HtmlGenericControl("div")
                .SetProperty(RenderSettings.ModeProperty, renderMode)
                .AddCssClass("test-class", bindingService.Cache.CreateValueBinding<bool>("Integer > 0", defaultDataContext));

            var str = RenderToString(control);
            Assert.AreEqual("""<div class=test-class data-bind='css: { "test-class": Integer() &gt; 0 }'></div>""", str);
        }

        [DataTestMethod]
        [DataRow(RenderMode.Client)]
        [DataRow(RenderMode.Server)]
        public void ValueBinding_Style(RenderMode renderMode) // RenderMode should not matter
        {
            var control = new HtmlGenericControl("div")
                .SetProperty(RenderSettings.ModeProperty, renderMode)
                .AddCssStyle("width", bindingService.Cache.CreateValueBinding<int>("Integer", defaultDataContext));

            var str = RenderToString(control);
            Assert.AreEqual("""<div style=width:123 data-bind="style: { width: Integer }"></div>""", str);
        }

        public class BasicTestViewModel: DotvvmViewModelBase
        {
            public int Integer { get; set; } = 123;
            public bool Boolean { get; set; } = false;
            public string String { get; set; } = "some-string";
        }
    }
}
