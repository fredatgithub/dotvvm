﻿using System;
using DotVVM.Framework.Compilation.Parser.Dothtml.Parser;
using DotVVM.Framework.Compilation.ControlTree;
using DotVVM.Framework.Compilation.Parser;
using DotVVM.Framework.Compilation.ControlTree.Resolved;
using DotVVM.Framework.Controls;
using DotVVM.Framework.Controls.Infrastructure;
using System.Linq;
using System.Collections.Immutable;

namespace DotVVM.Framework.Compilation.Directives
{
    using DirectiveDictionary = ImmutableDictionary<string, ImmutableList<DothtmlDirectiveNode>>;

    public class BaseTypeDirectiveCompiler : DirectiveCompiler<IAbstractBaseTypeDirective, ITypeDescriptor>
    {
        private readonly string fileName;
        private readonly ImmutableList<NamespaceImport> imports;

        public override string DirectiveName => ParserConstants.BaseTypeDirective;

        protected virtual ITypeDescriptor DotvvmViewType => new ResolvedTypeDescriptor(typeof(DotvvmView));
        protected virtual ITypeDescriptor DotvvmMarkupControlType => new ResolvedTypeDescriptor(typeof(DotvvmMarkupControl));

        public BaseTypeDirectiveCompiler(
            DirectiveDictionary directiveNodesByName, IAbstractTreeBuilder treeBuilder, string fileName, ImmutableList<NamespaceImport> imports)
            : base(directiveNodesByName, treeBuilder)
        {
            this.fileName = fileName;
            this.imports = imports;
        }

        protected override IAbstractBaseTypeDirective Resolve(DothtmlDirectiveNode directiveNode)
            => TreeBuilder.BuildBaseTypeDirective(directiveNode, ParseDirective(directiveNode, p => p.ReadDirectiveTypeName()), imports);

        protected override ITypeDescriptor CreateArtefact(ImmutableList<IAbstractBaseTypeDirective> resolvedDirectives)
        {
            var wrapperType = GetDefaultWrapperType();

            var baseControlDirective = resolvedDirectives.SingleOrDefault();

            if (baseControlDirective != null)
            {
                var baseType = baseControlDirective.ResolvedType;

                if (baseType == null)
                {
                    baseControlDirective.DothtmlNode!.AddError($"The type '{baseControlDirective.Value}' specified in baseType directive was not found!");
                }
                else if (!baseType.IsAssignableTo(DotvvmMarkupControlType))
                {
                    baseControlDirective.DothtmlNode!.AddError("Markup controls must derive from DotvvmMarkupControl class!");
                    wrapperType = baseType;
                }
                else if (baseType.GetControlMarkupOptionsAttribute() is { } attribute
                         && (!string.IsNullOrEmpty(attribute.PrimaryName) || attribute.AlternativeNames?.Any() == true))
                {
                    baseControlDirective.DothtmlNode!.AddError("Markup controls cannot use the PrimaryName or AlternativeNames properties in the ControlMarkupOptions attribute!");
                    wrapperType = baseType;
                }
                else
                {
                    wrapperType = baseType;
                }
            }

            return wrapperType;
        }

        /// <summary>
        /// Gets the default type of the wrapper for the view.
        /// </summary>
        private ITypeDescriptor GetDefaultWrapperType()
        {
            if (fileName.EndsWith(".dotcontrol", StringComparison.Ordinal))
            {
                return DotvvmMarkupControlType;
            }

            return DotvvmViewType;
        }
    }

}
