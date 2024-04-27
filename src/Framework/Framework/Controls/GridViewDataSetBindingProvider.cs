using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using DotVVM.Framework.Binding;
using DotVVM.Framework.Binding.Expressions;
using DotVVM.Framework.Binding.Properties;
using DotVVM.Framework.Compilation;
using DotVVM.Framework.Compilation.ControlTree;

namespace DotVVM.Framework.Controls;

public class GridViewDataSetBindingProvider
{
    private readonly BindingCompilationService service;

    private readonly ConcurrentDictionary<(DataContextStack, GridViewDataSetCommandType), DataPagerCommands> dataPagerCommands = new();
    private readonly ConcurrentDictionary<(DataContextStack, GridViewDataSetCommandType), GridViewCommands> gridViewCommands = new();

    public GridViewDataSetBindingProvider(BindingCompilationService service)
    {
        this.service = service;
    }

    public DataPagerCommands GetDataPagerCommands(DataContextStack dataContextStack, GridViewDataSetCommandType commandType)
    {
        return dataPagerCommands.GetOrAdd((dataContextStack, commandType), _ => GetDataPagerCommandsCore(dataContextStack, commandType));
    }

    public GridViewCommands GetGridViewCommands(DataContextStack dataContextStack, GridViewDataSetCommandType commandType)
    {
        return gridViewCommands.GetOrAdd((dataContextStack, commandType), _ => GetGridViewCommandsCore(dataContextStack, commandType));
    }

    private DataPagerCommands GetDataPagerCommandsCore(DataContextStack dataContextStack, GridViewDataSetCommandType commandType)
    {
        ICommandBinding? GetCommandOrNull<T>(ParameterExpression dataSetParam, DataContextStack dataContextStack, string methodName, params Expression[] arguments)
        {
            return typeof(T).IsAssignableFrom(dataSetParam.Type)
                ? CreateCommandBinding<T>(commandType, dataSetParam, dataContextStack, methodName, arguments)
                : null;
        }
        ParameterExpression CreateParameter(DataContextStack dataContextStack, string name = "_this")
        {
            return Expression.Parameter(dataContextStack.DataContextType, name).AddParameterAnnotation(new BindingParameterAnnotation(dataContextStack));
        }

        return new DataPagerCommands()
        {
            GoToFirstPage = GetCommandOrNull<IPageableGridViewDataSet<IPagingFirstPageCapability>>(
                CreateParameter(dataContextStack),
                dataContextStack,
                nameof(IPagingFirstPageCapability.GoToFirstPage)),

            GoToPreviousPage = GetCommandOrNull<IPageableGridViewDataSet<IPagingPreviousPageCapability>>(
                CreateParameter(dataContextStack),
                dataContextStack,
                nameof(IPagingPreviousPageCapability.GoToPreviousPage)),

            GoToNextPage = GetCommandOrNull<IPageableGridViewDataSet<IPagingNextPageCapability>>(
                CreateParameter(dataContextStack),
                dataContextStack,
                nameof(IPagingNextPageCapability.GoToNextPage)),

            GoToLastPage = GetCommandOrNull<IPageableGridViewDataSet<IPagingLastPageCapability>>(
                CreateParameter(dataContextStack),
                dataContextStack,
                nameof(IPagingLastPageCapability.GoToLastPage)),

            GoToPage = GetCommandOrNull<IPageableGridViewDataSet<IPagingPageIndexCapability>>(
                CreateParameter(dataContextStack, "_parent"),
                DataContextStack.Create(typeof(int), dataContextStack),
                nameof(IPagingPageIndexCapability.GoToPage),
                CreateParameter(DataContextStack.Create(typeof(int), dataContextStack), "_thisIndex"))
        };
    }

    private GridViewCommands GetGridViewCommandsCore(DataContextStack dataContextStack, GridViewDataSetCommandType commandType)
    {
        ICommandBinding? GetCommandOrNull<T>(ParameterExpression dataSetParam, string methodName, Expression[] arguments, Func<Expression, Expression> transformExpression)
        {
            return typeof(T).IsAssignableFrom(dataSetParam.Type)
                ? CreateCommandBinding<T>(commandType, dataSetParam, dataContextStack, methodName, arguments, transformExpression)
                : null;
        }
        ParameterExpression CreateParameter(DataContextStack dataContextStack)
        {
            return Expression.Parameter(dataContextStack.DataContextType).AddParameterAnnotation(new BindingParameterAnnotation(dataContextStack));
        }

        var setSortExpressionParam = Expression.Parameter(typeof(string));
        return new GridViewCommands()
        {
            SetSortExpression = GetCommandOrNull<ISortableGridViewDataSet<ISortingSetSortExpressionCapability>>(
                CreateParameter(dataContextStack),
                nameof(ISortingSetSortExpressionCapability.SetSortExpression),
                new Expression[] { setSortExpressionParam },
                // transform to sortExpression => command lambda
                e => Expression.Lambda(e, setSortExpressionParam))
        };
    }

    private ICommandBinding CreateCommandBinding<TDataSetInterface>(GridViewDataSetCommandType commandType, ParameterExpression dataSetParam, DataContextStack dataContextStack, string methodName, Expression[] arguments, Func<Expression, Expression>? transformExpression = null)
    {
        var body = new List<Expression>();

        // get concrete type from implementation of IXXXableGridViewDataSet<?>
        var optionsConcreteType = GetOptionsConcreteType<TDataSetInterface>(dataSetParam.Type, out var optionsProperty);

        // call dataSet.XXXOptions.Method(...);
        var callMethodOnOptions = Expression.Call(
            Expression.Convert(Expression.Property(dataSetParam, optionsProperty), optionsConcreteType),
            optionsConcreteType.GetMethod(methodName)!,
            arguments);
        body.Add(callMethodOnOptions);

        if (commandType == GridViewDataSetCommandType.Command)
        {
            // if we are on a server, call the dataSet.RequestRefresh if supported
            if (typeof(IRefreshableGridViewDataSet).IsAssignableFrom(dataSetParam.Type))
            {
                var callRequestRefresh = Expression.Call(
                    Expression.Convert(dataSetParam, typeof(IRefreshableGridViewDataSet)),
                    typeof(IRefreshableGridViewDataSet).GetMethod(nameof(IRefreshableGridViewDataSet.RequestRefresh))!
                );
                body.Add(callRequestRefresh);
            }

            // build command binding
            Expression expression = Expression.Block(body);
            if (transformExpression != null)
            {
                expression = transformExpression(expression);
            }
            return new CommandBindingExpression(service,
                new object[]
                {
                    new ParsedExpressionBindingProperty(expression),
                    new OriginalStringBindingProperty($"DataPager: _dataSet.{methodName}({string.Join(", ", arguments.AsEnumerable())})"), // For ID generation
                    dataContextStack
                });
        }
        else if (commandType == GridViewDataSetCommandType.StaticCommand)
        {
            // on the client, wrap the call into client-side loading procedure
            body.Add(CallClientSideLoad(dataSetParam));
            Expression expression = Expression.Block(body);
            if (transformExpression != null)
            {
                expression = transformExpression(expression);
            }
            return new StaticCommandBindingExpression(service,
                new object[]
                {
                    new ParsedExpressionBindingProperty(expression),
                    BindingParserOptions.StaticCommand,
                    dataContextStack
                });
        }
        else
        {
            throw new NotSupportedException($"The command type {commandType} is not supported!");
        }
    }
    
    /// <summary>
    /// Invoked the client-side loadDataSet function with the loader from $gridViewDataSetHelper
    /// </summary>
    private static Expression CallClientSideLoad(Expression dataSetParam)
    {
        // call static method DataSetClientLoad
        var method = typeof(GridViewDataSetBindingProvider).GetMethod(nameof(DataSetClientSideLoad))!;
        return Expression.Call(method, dataSetParam);
    }

    private static Expression CurrentGridDataSetExpression(Type datasetType)
    {
        var method = typeof(GridViewDataSetBindingProvider).GetMethod(nameof(GetCurrentGridDataSet))!.MakeGenericMethod(datasetType);
        return Expression.Call(method);
    }

    /// <summary>
    /// A sentinel method which is translated to load the GridViewDataSet on the client side using the Load delegate.
    /// Do not call this method on the server.
    /// </summary>
    public static Task DataSetClientSideLoad(IBaseGridViewDataSet dataSet)
    {
        throw new InvalidOperationException("This method cannot be called on the server!");
    }

    /// <summary> Returns the DataSet we currently work on from the $context.$gridViewDataSetHelper.dataSet </summary>
    public static T GetCurrentGridDataSet<T>() where T : IBaseGridViewDataSet
    {
        throw new InvalidOperationException("This method cannot be called on the server!");
    }

    private static Type GetOptionsConcreteType<TDataSetInterface>(Type dataSetConcreteType, out PropertyInfo optionsProperty)
    {
        if (!typeof(TDataSetInterface).IsGenericType || !typeof(TDataSetInterface).IsAssignableFrom(dataSetConcreteType))
        {
            throw new ArgumentException($"The type {typeof(TDataSetInterface)} must be a generic type and must be implemented by the type {dataSetConcreteType} specified in {nameof(dataSetConcreteType)} argument!");
        }

        // resolve options property
        var genericInterface = typeof(TDataSetInterface).GetGenericTypeDefinition();
        if (genericInterface == typeof(IFilterableGridViewDataSet<>))
        {
            optionsProperty = typeof(TDataSetInterface).GetProperty(nameof(IFilterableGridViewDataSet.FilteringOptions))!;
        }
        else if (genericInterface == typeof(ISortableGridViewDataSet<>))
        {
            optionsProperty = typeof(TDataSetInterface).GetProperty(nameof(ISortableGridViewDataSet.SortingOptions))!;
        }
        else if (genericInterface == typeof(IPageableGridViewDataSet<>))
        {
            optionsProperty = typeof(TDataSetInterface).GetProperty(nameof(IPageableGridViewDataSet.PagingOptions))!;
        }
        else
        {
            throw new ArgumentException($"The {typeof(TDataSetInterface)} can only be {nameof(IFilterableGridViewDataSet)}, {nameof(ISortableGridViewDataSet)} or {nameof(IPageableGridViewDataSet)} with one generic argument!");
        }

        var interfaces = dataSetConcreteType.GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == genericInterface)
            .ToList();
        if (interfaces.Count < 1)
        {
            throw new ArgumentException($"The {dataSetConcreteType} doesn't implement {genericInterface.Name}<TOptions>.");
        }
        else if (interfaces.Count > 1)
        {
            throw new ArgumentException($"The {dataSetConcreteType} implements multiple interfaces where {genericInterface.Name}<TOptions>. Only one implementation is allowed.");
        }

        var pagingOptionsConcreteType = interfaces[0].GetGenericArguments()[0];
        return pagingOptionsConcreteType;
    }
}
