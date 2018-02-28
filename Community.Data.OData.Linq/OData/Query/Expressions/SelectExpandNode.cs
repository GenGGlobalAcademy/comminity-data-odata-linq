﻿// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

namespace Community.OData.Linq.OData.Query.Expressions
{
    using System.Collections.Generic;
    using System.Linq;

    using Community.OData.Linq.Builder;
    using Community.OData.Linq.Common;
    using Community.OData.Linq.Properties;

    using Microsoft.OData;
    using Microsoft.OData.Edm;
    using Microsoft.OData.UriParser;

    /// <summary>
    /// Describes the set of structural properties and navigation properties and actions to select and navigation properties to expand while 
    /// writing an <see cref="ODataResource"/> in the response.
    /// </summary>
    public class SelectExpandNode
    {
        /// <summary>
        /// Creates a new instance of the <see cref="SelectExpandNode"/> class.
        /// </summary>
        /// <remarks>The default constructor is for unit testing only.</remarks>
        public SelectExpandNode()
        {
            this.SelectedStructuralProperties = new HashSet<IEdmStructuralProperty>();
            this.SelectedComplexProperties = new HashSet<IEdmStructuralProperty>();
            this.SelectedNavigationProperties = new HashSet<IEdmNavigationProperty>();
            this.ExpandedNavigationProperties = new Dictionary<IEdmNavigationProperty, SelectExpandClause>();
            this.SelectedActions = new HashSet<IEdmAction>();
            this.SelectedFunctions = new HashSet<IEdmFunction>();
            this.SelectedDynamicProperties = new HashSet<string>();
        }

        /// <summary>
        /// Creates a new instance of the <see cref="SelectExpandNode"/> class by copying the state of another instance. This is
        /// intended for scenarios that wish to modify state without updating the values cached within ODataResourceSerializer.
        /// </summary>
        /// <param name="selectExpandNodeToCopy">The instance from which the state for the new instance will be copied.</param>
        public SelectExpandNode(SelectExpandNode selectExpandNodeToCopy)
        {
            this.ExpandedNavigationProperties = new Dictionary<IEdmNavigationProperty, SelectExpandClause>(selectExpandNodeToCopy.ExpandedNavigationProperties);
            this.SelectedActions = new HashSet<IEdmAction>(selectExpandNodeToCopy.SelectedActions);
            this.SelectAllDynamicProperties = selectExpandNodeToCopy.SelectAllDynamicProperties;
            this.SelectedComplexProperties = new HashSet<IEdmStructuralProperty>(selectExpandNodeToCopy.SelectedComplexProperties);
            this.SelectedDynamicProperties = new HashSet<string>(selectExpandNodeToCopy.SelectedDynamicProperties);
            this.SelectedFunctions = new HashSet<IEdmFunction>(selectExpandNodeToCopy.SelectedFunctions);
            this.SelectedNavigationProperties = new HashSet<IEdmNavigationProperty>(selectExpandNodeToCopy.SelectedNavigationProperties);
            this.SelectedStructuralProperties = new HashSet<IEdmStructuralProperty>(selectExpandNodeToCopy.SelectedStructuralProperties);
        }

        /// <summary>
        /// Creates a new instance of the <see cref="SelectExpandNode"/> class describing the set of structural properties,
        /// nested properties, navigation properties, and actions to select and expand for the given <paramref name="selectExpandClause"/>.
        /// </summary>
        /// <param name="selectExpandClause">The parsed $select and $expand query options.</param>
        /// <param name="structuredType">The structural type of the resource that would be written.</param>
        /// <param name="model">The <see cref="IEdmModel"/> that contains the given structural type.</param>
        public SelectExpandNode(SelectExpandClause selectExpandClause, IEdmStructuredType structuredType, IEdmModel model)
            : this()
        {
            if (structuredType == null)
            {
                throw Error.ArgumentNull("structuredType");
            }

            if (model == null)
            {
                throw Error.ArgumentNull("model");
            }

            // So far, it includes all properties of primitive, enum and collection of them
            HashSet<IEdmStructuralProperty> allStructuralProperties = new HashSet<IEdmStructuralProperty>();

            // So far, it includes all properties of complex and collection of complex
            HashSet<IEdmStructuralProperty> allComplexStructuralProperties = new HashSet<IEdmStructuralProperty>();
            GetStructuralProperties(structuredType, allStructuralProperties, allComplexStructuralProperties);

            // So far, it includes all navigation properties
            HashSet<IEdmNavigationProperty> allNavigationProperties;
            HashSet<IEdmAction> allActions;
            HashSet<IEdmFunction> allFunctions;

            IEdmEntityType entityType = structuredType as IEdmEntityType;
            if (entityType != null)
            {
                allNavigationProperties = new HashSet<IEdmNavigationProperty>(entityType.NavigationProperties());
                allActions = new HashSet<IEdmAction>(model.GetAvailableActions(entityType));
                allFunctions = new HashSet<IEdmFunction>(model.GetAvailableFunctions(entityType));
            }
            else
            {
                allNavigationProperties = new HashSet<IEdmNavigationProperty>();
                allActions = new HashSet<IEdmAction>();
                allFunctions = new HashSet<IEdmFunction>();
            }

            if (selectExpandClause == null)
            {
                this.SelectedStructuralProperties = allStructuralProperties;
                this.SelectedComplexProperties = allComplexStructuralProperties;
                this.SelectedNavigationProperties = allNavigationProperties;
                this.SelectedActions = allActions;
                this.SelectedFunctions = allFunctions;
                this.SelectAllDynamicProperties = true;
            }
            else
            {
                if (selectExpandClause.AllSelected)
                {
                    this.SelectedStructuralProperties = allStructuralProperties;
                    this.SelectedComplexProperties = allComplexStructuralProperties;
                    this.SelectedNavigationProperties = allNavigationProperties;
                    this.SelectedActions = allActions;
                    this.SelectedFunctions = allFunctions;
                    this.SelectAllDynamicProperties = true;
                }
                else
                {
                    this.BuildSelections(selectExpandClause, allStructuralProperties, allComplexStructuralProperties, allNavigationProperties, allActions, allFunctions);
                    this.SelectAllDynamicProperties = false;
                }

                this.BuildExpansions(selectExpandClause, allNavigationProperties);

                // remove expanded navigation properties from the selected navigation properties.
                this.SelectedNavigationProperties.ExceptWith(this.ExpandedNavigationProperties.Keys);
            }
        }

        /// <summary>
        /// Gets the list of EDM structural properties (primitive, enum or collection of them) to be included in the response.
        /// </summary>
        public ISet<IEdmStructuralProperty> SelectedStructuralProperties { get; private set; }

        /// <summary>
        /// Gets the list of EDM navigation properties to be included as links in the response.
        /// </summary>
        public ISet<IEdmNavigationProperty> SelectedNavigationProperties { get; private set; }

        /// <summary>
        /// Gets the list of EDM navigation properties to be expanded in the response.
        /// </summary>
        public IDictionary<IEdmNavigationProperty, SelectExpandClause> ExpandedNavigationProperties { get; private set; }

        /// <summary>
        /// Gets the list of EDM nested properties (complex or collection of complex) to be included in the response.
        /// </summary>
        public ISet<IEdmStructuralProperty> SelectedComplexProperties { get; private set; }

        /// <summary>
        /// Gets the list of dynamic properties to select.
        /// </summary>
        public ISet<string> SelectedDynamicProperties { get; private set; }

        /// <summary>
        /// Gets the flag to indicate the dynamic property to be included in the response or not.
        /// </summary>
        public bool SelectAllDynamicProperties { get; private set; }

        /// <summary>
        /// Gets the list of OData actions to be included in the response.
        /// </summary>
        public ISet<IEdmAction> SelectedActions { get; private set; }

        /// <summary>
        /// Gets the list of OData functions to be included in the response.
        /// </summary>
        public ISet<IEdmFunction> SelectedFunctions { get; private set; }

        private void BuildExpansions(SelectExpandClause selectExpandClause, HashSet<IEdmNavigationProperty> allNavigationProperties)
        {
            foreach (SelectItem selectItem in selectExpandClause.SelectedItems)
            {
                ExpandedNavigationSelectItem expandItem = selectItem as ExpandedNavigationSelectItem;
                if (expandItem != null)
                {
                    ValidatePathIsSupported(expandItem.PathToNavigationProperty);
                    NavigationPropertySegment navigationSegment = (NavigationPropertySegment)expandItem.PathToNavigationProperty.LastSegment;
                    IEdmNavigationProperty navigationProperty = navigationSegment.NavigationProperty;
                    if (allNavigationProperties.Contains(navigationProperty))
                    {
                        this.ExpandedNavigationProperties.Add(navigationProperty, expandItem.SelectAndExpand);
                    }
                }
            }
        }

        private void BuildSelections(
            SelectExpandClause selectExpandClause,
            HashSet<IEdmStructuralProperty> allStructuralProperties,
            HashSet<IEdmStructuralProperty> allNestedProperties,
            HashSet<IEdmNavigationProperty> allNavigationProperties,
            HashSet<IEdmAction> allActions,
            HashSet<IEdmFunction> allFunctions)
        {
            foreach (SelectItem selectItem in selectExpandClause.SelectedItems)
            {
                if (selectItem is ExpandedNavigationSelectItem)
                {
                    continue;
                }

                PathSelectItem pathSelectItem = selectItem as PathSelectItem;
                if (pathSelectItem != null)
                {
                    ValidatePathIsSupported(pathSelectItem.SelectedPath);
                    ODataPathSegment segment = pathSelectItem.SelectedPath.LastSegment;

                    NavigationPropertySegment navigationPropertySegment = segment as NavigationPropertySegment;
                    if (navigationPropertySegment != null)
                    {
                        IEdmNavigationProperty navigationProperty = navigationPropertySegment.NavigationProperty;
                        if (allNavigationProperties.Contains(navigationProperty))
                        {
                            this.SelectedNavigationProperties.Add(navigationProperty);
                        }
                        continue;
                    }

                    PropertySegment structuralPropertySegment = segment as PropertySegment;
                    if (structuralPropertySegment != null)
                    {
                        IEdmStructuralProperty structuralProperty = structuralPropertySegment.Property;
                        if (allStructuralProperties.Contains(structuralProperty))
                        {
                            this.SelectedStructuralProperties.Add(structuralProperty);
                        }
                        else if (allNestedProperties.Contains(structuralProperty))
                        {
                            this.SelectedComplexProperties.Add(structuralProperty);
                        }
                        continue;
                    }

                    OperationSegment operationSegment = segment as OperationSegment;
                    if (operationSegment != null)
                    {
                        this.AddOperations(allActions, allFunctions, operationSegment);
                        continue;
                    }

                    DynamicPathSegment dynamicPathSegment = segment as DynamicPathSegment;
                    if (dynamicPathSegment != null)
                    {
                        this.SelectedDynamicProperties.Add(dynamicPathSegment.Identifier);
                        continue;
                    }
                    throw new ODataException(Error.Format(SRResources.SelectionTypeNotSupported, segment.GetType().Name));
                }

                WildcardSelectItem wildCardSelectItem = selectItem as WildcardSelectItem;
                if (wildCardSelectItem != null)
                {
                    this.SelectedStructuralProperties = allStructuralProperties;
                    this.SelectedComplexProperties = allNestedProperties;
                    this.SelectedNavigationProperties = allNavigationProperties;
                    continue;
                }

                NamespaceQualifiedWildcardSelectItem wildCardActionSelection = selectItem as NamespaceQualifiedWildcardSelectItem;
                if (wildCardActionSelection != null)
                {
                    this.SelectedActions = allActions;
                    this.SelectedFunctions = allFunctions;
                    continue;
                }

                throw new ODataException(Error.Format(SRResources.SelectionTypeNotSupported, selectItem.GetType().Name));
            }
        }

        private void AddOperations(HashSet<IEdmAction> allActions, HashSet<IEdmFunction> allFunctions, OperationSegment operationSegment)
        {
            foreach (IEdmOperation operation in operationSegment.Operations)
            {
                IEdmAction action = operation as IEdmAction;
                if (action != null && allActions.Contains(action))
                {
                    this.SelectedActions.Add(action);
                }

                IEdmFunction function = operation as IEdmFunction;
                if (function != null && allFunctions.Contains(function))
                {
                    this.SelectedFunctions.Add(function);
                }
            }
        }

        // we only support paths of type 'cast/structuralOrNavPropertyOrAction' and 'structuralOrNavPropertyOrAction'.
        internal static void ValidatePathIsSupported(ODataPath path)
        {
            int segmentCount = path.Count();

            if (segmentCount > 2)
            {
                throw new ODataException(SRResources.UnsupportedSelectExpandPath);
            }

            if (segmentCount == 2)
            {
                if (!(path.FirstSegment is TypeSegment))
                {
                    throw new ODataException(SRResources.UnsupportedSelectExpandPath);
                }
            }

            ODataPathSegment lastSegment = path.LastSegment;
            if (!(lastSegment is NavigationPropertySegment
                || lastSegment is PropertySegment
                || lastSegment is OperationSegment
                || lastSegment is DynamicPathSegment))
            {
                throw new ODataException(SRResources.UnsupportedSelectExpandPath);
            }
        }

        /// <summary>
        /// Separate the structural properties into two parts:
        /// 1. Complex and collection of complex are nested structural properties.
        /// 2. Others are non-nested structural properties.
        /// </summary>
        /// <param name="structuredType">The structural type of the resource.</param>
        /// <param name="structuralProperties">The non-nested structural properties of the structural type.</param>
        /// <param name="nestedStructuralProperties">The nested structural properties of the structural type.</param>
        public static void GetStructuralProperties(IEdmStructuredType structuredType, HashSet<IEdmStructuralProperty> structuralProperties,
            HashSet<IEdmStructuralProperty> nestedStructuralProperties)
        {
            if (structuredType == null)
            {
                throw Error.ArgumentNull("structuredType");
            }

            if (structuralProperties == null)
            {
                throw Error.ArgumentNull("structuralProperties");
            }

            if (nestedStructuralProperties == null)
            {
                throw Error.ArgumentNull("nestedStructuralProperties");
            }

            foreach (var edmStructuralProperty in structuredType.StructuralProperties())
            {
                if (edmStructuralProperty.Type.IsComplex())
                {
                    nestedStructuralProperties.Add(edmStructuralProperty);
                }
                else if (edmStructuralProperty.Type.IsCollection())
                {
                    if (edmStructuralProperty.Type.AsCollection().ElementType().IsComplex())
                    {
                        nestedStructuralProperties.Add(edmStructuralProperty);
                    }
                    else
                    {
                        structuralProperties.Add(edmStructuralProperty);
                    }
                }
                else
                {
                    structuralProperties.Add(edmStructuralProperty);
                }
            }
        }
    }
}
