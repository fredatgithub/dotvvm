﻿using System.Linq;
using DotVVM.Framework.Compilation.ControlTree.Resolved;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DotVVM.Framework.Utils;
using System;
using DotVVM.Framework.Testing;
using System.Collections.Generic;
using DotVVM.Framework.Compilation;

namespace DotVVM.Framework.Tests.Runtime.ControlTree
{
    [TestClass]
    public class MarkupDeclaredPropertyTests : DefaultControlTreeResolverTestsBase
    {
        [TestMethod]
        public void ResolvedTree_MarkupDeclaredProperty_PropertyCorrectTypeAndName()
        {
            var root = ParseSource(@"@viewModel object
@property string Heading
",
fileName: "control.dotcontrol");

            var declarationDirective = EnsureSingleResolvedDeclarationDirective(root);

            Assert.IsFalse(declarationDirective.DothtmlNode.HasNodeErrors);
            Assert.AreEqual(typeof(string).FullName, declarationDirective.PropertyType.FullName);
            Assert.AreEqual("Heading", declarationDirective.NameSyntax.Name);
        }

        [DataTestMethod]
        [DataRow("true", "bool", typeof(bool), true)]
        [DataRow("30", "int", typeof(int), 30)]
        [DataRow("10", "long", typeof(long), (long)10)]
        [DataRow("3.14", "double", typeof(double), 3.14)]
        [DataRow("'a'", "char", typeof(char), 'a')]
        [DataRow("\"t\"+\"e\"+$\"{\"s\"}t{1+1}\"+DotVVM.Framework.Tests.Runtime.ControlTree.TestConstants.Test", "string", typeof(string), "test2test")]
        public void ResolvedTree_MarkupDeclaredProperty_CorrectConstantInitialValue(string initializer, string typeName, Type propertyType, object testedValue)
        {
            var root = ParseSource(@$"@viewModel object
@property {typeName} IsClosed = {initializer}
",
fileName: "control.dotcontrol");
            var declarationDirective = EnsureSingleResolvedDeclarationDirective(root);

            Assert.IsFalse(declarationDirective.DothtmlNode.HasNodeErrors);
            Assert.AreEqual(propertyType.FullName, declarationDirective.PropertyType.FullName);
            Assert.AreEqual("IsClosed", declarationDirective.NameSyntax.Name);

            Assert.AreEqual(testedValue, declarationDirective.InitialValue);
        }

        [TestMethod]
        public void ResolvedTree_MarkupDeclaredProperty_ArrayInitializer_Array()
        {
            var root = ParseSource(@$"@viewModel object
@property int[] Items = [1, 1+1, 9/3, 9%5, 2*3-1, DotVVM.Framework.Tests.Runtime.ControlTree.TestConstants.Six]
",
fileName: "control.dotcontrol");
            var declarationDirective = EnsureSingleResolvedDeclarationDirective(root);

            Assert.IsFalse(declarationDirective.DothtmlNode.HasNodeErrors);
            Assert.AreEqual(typeof(int[]).FullName, declarationDirective.PropertyType.FullName);
            Assert.AreEqual("Items", declarationDirective.NameSyntax.Name);
            
            Assert.IsInstanceOfType(declarationDirective.InitialValue, typeof(int[]));
            CollectionAssert.AreEqual(new int[] { 1, 2, 3, 4, 5, 6 }, (int[])declarationDirective.InitialValue);
        }

        [TestMethod]
        public void ResolvedTree_MarkupDeclaredProperty_ArrayInitializer_List()
        {
            var root = ParseSource(@$"@viewModel object
@property System.Collections.Generic.List<int> Items = [1, 2, 3, 4, 5, DotVVM.Framework.Tests.Runtime.ControlTree.TestConstants.Six]
",
fileName: "control.dotcontrol");
            var declarationDirective = EnsureSingleResolvedDeclarationDirective(root);

            Assert.IsTrue(declarationDirective.DothtmlNode.HasNodeErrors);
            Assert.IsTrue(declarationDirective.DothtmlNode.NodeErrors.Any(e => e.Contains("initialize") && e.Contains("value")));
            Assert.IsTrue(declarationDirective.DothtmlNode.NodeErrors.Any(e => e.Contains("type")));

            Assert.AreEqual(typeof(List<int>).FullName, declarationDirective.PropertyType.FullName);
            Assert.AreEqual("Items", declarationDirective.NameSyntax.Name);
        }

        [TestMethod]
        public void ResolvedTree_MarkupDeclaredProperty_ImportUsed()
        {
            var root = ParseSource(@$"
@viewModel object
@import System.Collections.Generic
@property List<int> Items

<dot:Repeater DataSource={{value: _control.Items}}>
   <li>{{{{value: _this}}}}</li>
</dot:Repeater>
",
fileName: "control.dotcontrol");

            CheckPropertyAndBinding(root, typeof(List<int>), "Items");
        }

       
        [TestMethod]
        public void ResolvedTree_MarkupDeclaredProperty_GlobalImportUsed()
        {
            var config = DotvvmTestHelper.CreateConfiguration();
            config.Markup.ImportedNamespaces.Add(new NamespaceImport("System.Collections.Generic"));
            config.Markup.ImportedNamespaces.Add(new NamespaceImport("System"));

            var root = DotvvmTestHelper.ParseResolvedTree(@$"
@viewModel object
@property List<Guid> Items

<dot:Repeater DataSource={{value: _control.Items}}>
   <li>{{{{value: _this}}}}</li>
</dot:Repeater>
",
fileName: "control.dotcontrol", configuration: config);

            CheckPropertyAndBinding(root, typeof(List<Guid>), "Items");
        }

        [DataTestMethod]
        [DataRow("true", "System.Collections.Generic.List<int>", typeof(List<int>), default(List<int>))]
        [DataRow("30", "bool", typeof(bool), default(bool))]
        [DataRow("\"test\"", "long", typeof(long), default(long))]
        [DataRow("3.14", "string", typeof(string), default(string))]
        [DataRow("\"test\"", "int []", typeof(int []), default(int []))]
        [DataRow("[ 1, 2, 3, 4 ]", "string []", typeof(string[]), default(string[]))]
        public void ResolvedTree_MarkupDeclaredProperty_IncompatibleTypes(string initializer, string typeName, Type propertyType, object testedValue)
        {
            var root = ParseSource(@$"@viewModel object
@property {typeName} MisstypedProperty = {initializer}
",
fileName: "control.dotcontrol");
            var declarationDirective = EnsureSingleResolvedDeclarationDirective(root);

            Assert.AreEqual(propertyType.FullName, declarationDirective.PropertyType.FullName);
            Assert.AreEqual(testedValue, declarationDirective.InitialValue);

            Assert.IsTrue(declarationDirective.DothtmlNode.HasNodeErrors);
            Assert.IsTrue(declarationDirective.DothtmlNode.NodeErrors.Any(e => e.Contains("initialize") && e.Contains("value")));
            Assert.IsTrue(declarationDirective.DothtmlNode.NodeErrors.Any(e=> e.Contains("type")));
        }

        [DataTestMethod]
        [DataRow("[ 1, '', '', '' ]", "string []")]
        [DataRow("[ 1, 0.0, 0.0, 0.0 ]", "int []")]
        public void ResolvedTree_MarkupDeclaredProperty_ElementTypeCannotBeDetermined(string initializer, string typeName)
        {
            var root = ParseSource(@$"@viewModel object
@property {typeName} MisstypedProperty = {initializer}
",
fileName: "control.dotcontrol");
            var declarationDirective = EnsureSingleResolvedDeclarationDirective(root);

            Assert.IsTrue(declarationDirective.DothtmlNode.HasNodeErrors);
            Assert.IsTrue(declarationDirective.DothtmlNode.NodeErrors.Any(e => e.Contains("initialize") && e.Contains("value")));
            Assert.IsTrue(declarationDirective.DothtmlNode.NodeErrors.Any(e => e.Contains("same type")));
        }

        private static void CheckPropertyAndBinding(ResolvedTreeRoot root, Type expectedType, string expectedName)
        {
            var declarationDirective = EnsureSingleResolvedDeclarationDirective(root);
            var binding = EnsureSingleResolvedBinding(root);

            Assert.IsFalse(declarationDirective.DothtmlNode.HasNodeErrors);
            Assert.AreEqual(expectedType.FullName, declarationDirective.PropertyType.FullName);
            Assert.AreEqual(expectedName, declarationDirective.NameSyntax.Name);

            Assert.IsNotNull(binding.ResultType);
            Assert.AreEqual(expectedType.FullName, binding.ResultType.FullName);
        }

        private static ResolvedPropertyDeclarationDirective EnsureSingleResolvedDeclarationDirective(ResolvedTreeRoot root) =>
                root.Directives["property"]
                    .Single()
                    .CastTo<ResolvedPropertyDeclarationDirective>();

        private static ResolvedBinding EnsureSingleResolvedBinding(ResolvedTreeRoot root) =>
              root.Content[0].CastTo<ResolvedControl>()
                   .Content[2].CastTo<ResolvedControl>()
                   .Properties.First().Value.CastTo<ResolvedPropertyBinding>()
                   .Binding;
    }

    public class TestConstants
    {
        public const string Test = "test";
        public const int Six = 6;
    }
}
