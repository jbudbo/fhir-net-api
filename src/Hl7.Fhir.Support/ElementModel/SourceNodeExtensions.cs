﻿using System.Linq;
using System.Collections.Generic;
using System;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Utility;
using Hl7.Fhir.Specification;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.ElementModel.Adapters;

namespace Hl7.Fhir.ElementModel
{

    public static class SourceNodeExtensions
    {
        /// <summary>
        /// Returns the direct children of a set of nodes.
        /// </summary>
        /// <param name="node">A list of nodes.</param>
        /// <param name="name"> Optional.The name filter for the children. Can be omitted to not filter by name.</param>
        /// <returns>The children of all nodes passed into <paramref name="node"/>, aggregated into a single list.</returns>
        public static IEnumerable<ISourceNode> Children(this IEnumerable<ISourceNode> node, string name = null) =>
            node == null ? throw Error.ArgumentNull(nameof(node)) :
            node.SelectMany(n => n.Children(name));

        /// <summary>
        /// Returns all descendants of a node.
        /// </summary>
        /// <param name="node">A node.</param>
        /// <returns>The descendants (children and by recursion all children of the children) of the node passed into 
        /// <paramref name="node"/></returns>
        public static IEnumerable<ISourceNode> Descendants(this ISourceNode node)
        {
            if (node == null) throw Error.ArgumentNull(nameof(node));

            foreach (var child in node.Children())
            {
                yield return child;
                foreach (var grandchild in child.Descendants()) yield return grandchild;
            }
        }

        /// <summary>
        /// Returns all descendants of a set of nodes.
        /// </summary>
        /// <param name="nodes">A list of nodes.</param>
        /// <returns>The descendants (children and by recursion all children of the children) of the node passed into 
        /// <paramref name="nodes"/></returns>
        /// <returns>The descendants of all nodes passed into <paramref name="nodes"/>, aggregated into a single list.</returns>
        public static IEnumerable<ISourceNode> Descendants(this IEnumerable<ISourceNode> nodes) =>
            nodes == null ? throw Error.ArgumentNull(nameof(nodes)) :
            nodes.SelectMany(n => n.Descendants());


        /// <summary>
        /// Returns a node and all descendants of that node.
        /// </summary>
        /// <param name="node">A node.</param>
        /// <returns>The node and descendants (children and by recursion all children of the children) of the node passed into 
        /// <paramref name="node"/></returns>
        public static IEnumerable<ISourceNode> DescendantsAndSelf(this ISourceNode node)
        {
            if (node == null) throw Error.ArgumentNull(nameof(node));
            return (new[] { node }).Concat(node.Descendants());
        }

        /// <summary>
        /// Returns nodes and all descendants of those nodes from a set of nodes.
        /// </summary>
        /// <param name="nodes">A list of nodes.</param>
        /// <returns>The node and descendants (children and by recursion all children of the children) of all
        /// nodes passed into <paramref name="nodes"/></returns>
        public static IEnumerable<ISourceNode> DescendantsAndSelf(this IEnumerable<ISourceNode> nodes)
        {
            if (nodes == null) throw Error.ArgumentNull(nameof(nodes));
            return nodes.SelectMany(n => n.DescendantsAndSelf());
        }

        /// <summary>
        /// Runs an action on all nodes in a tree of nodes.
        /// </summary>
        /// <param name="root">The root of the tree.</param>
        /// <param name="visitor">The action to run on each node.</param>
        /// <remarks>The function does a depth-first traversal of the tree, starting at the root. The action is
        /// passed an integer representing the depth of the node in the tree, measured in steps from the node
        /// to the root.</remarks>
        public static void Visit(this ISourceNode root, Action<int, ISourceNode> visitor)
        {
            if (root == null) throw Error.ArgumentNull(nameof(root));
            if (visitor == null) throw Error.ArgumentNull(nameof(visitor));

            root.visit(visitor, 0);
        }

        private static void visit(this ISourceNode navigator, Action<int, ISourceNode> visitor, int depth = 0)
        {
            visitor(depth, navigator);
            foreach (var child in navigator.Children())
            {
                visit(child, visitor, depth + 1);
            }
        }

        /// <summary>
        /// Registers an <see cref="ExceptionNotificationHandler" /> with an <see cref="IExceptionSource"/>.
        /// </summary>
        /// <seealso cref="ExceptionSourceExtensions.Catch(IExceptionSource, ExceptionNotificationHandler, bool)"/>
        public static IDisposable Catch(this ISourceNode node, ExceptionNotificationHandler handler, bool forward = false)
        {
            if (node == null) throw Error.ArgumentNull(nameof(node));
            if (handler == null) throw Error.ArgumentNull(nameof(handler));

            return node is IExceptionSource s ?
                s.Catch(handler, forward)
                : throw new NotImplementedException("Node does not implement IExceptionSource.");
        }


        /// <summary>
        /// Visit all nodes in a tree while invoking the <see cref="ISourceNode.Text" /> getter. />
        /// </summary>
        /// <param name="root">The root of the tree to visit.</param>
        /// <remarks>Since implementations of ISourceNode will report parsing errors when enumerating
        /// children and getting their <see cref="ISourceNode.Text"/> getter, this will trigger all
        /// parsing errors to be reported by the source.</remarks>
        public static void VisitAll(this ISourceNode root)
        {
            if (root == null) throw Error.ArgumentNull(nameof(root));
            root.Visit((_, n) => { var dummy = n.Text; });
        }

        /// <summary>
        /// Visit all nodes in a tree while catching any reported parsing errors. />
        /// </summary>
        /// <param name="root">The root of the tree to visit.</param>
        /// <returns>The list of all exceptions reported while visiting the tree passed in
        /// the <paramref name="root"/> argument.</returns>
        /// <seealso cref="VisitAll(ISourceNode)"/>
        public static IList<ExceptionNotification> VisitAndCatch(this ISourceNode root)
        {
            if (root == null) throw Error.ArgumentNull(nameof(root));

            var errors = new List<ExceptionNotification>();

            using (root.Catch((o, arg) => errors.Add(arg)))
            {
                root.VisitAll();
            }

            return errors;
        }


        /// <summary>
        /// Gets specific annotations from the list of annotations on the node.
        /// </summary>
        /// <returns>All of the annotations of the given type, or an empty list if none were found.</returns>
        /// <seealso cref="IAnnotated"/>
        public static IEnumerable<object> Annotations(this ISourceNode node, Type type) =>
            node is IAnnotated ann ? ann.Annotations(type) : Enumerable.Empty<object>();

        /// <summary>
        /// Gets a specific annotation from the list of annotations on the node.
        /// </summary>
        /// <returns>The first of the annotations of the type given by <typeparamref name="T"/>,
        /// or an empty list if none were found.</returns>
        /// <seealso cref="IAnnotated"/>
        public static T Annotation<T>(this ISourceNode nav) where T : class =>
            nav is IAnnotated ann ? ann.Annotation<T>() : null;

        public static ITypedElement ToTypedNode(this ISourceNode sourceNav, IStructureDefinitionSummaryProvider provider, string type = null, TypedNodeSettings settings = null)
        {
            if (provider == null) throw Error.ArgumentNull(nameof(provider));
            return new TypedElement(sourceNav, type, provider, settings: settings);
        }

#pragma warning disable 612, 618
        /// <summary>
        /// Adapts the node to implement the <see cref="IElementNavigator"/> interface.
        /// </summary>
        /// <param name="node">The node to be adapted.</param>
        /// <returns>An implementation of <see cref="IElementNavigator"/> on top of the node passed in.</returns>
        /// <remarks>Only to be used for backwards compatibility purposes, where an <see cref="IElementNavigator"/> is needed
        /// but only the newer <c>ISourceNode</c> is available. Note that since there is no type information available
        /// on <c>ISourceNode</c>, components depending on type information that is supposed to be present on
        /// <c>IElementNavigator</c> may fail.</remarks>
        [Obsolete("Turning an untyped SourceNode into a typed ElementNavigator without providing" +
            "type information (see other overload) will cause side-effects with components in the API that are not " +
            "prepared to deal with missing type information.")]
        public static IElementNavigator ToElementNavigator(this ISourceNode node) =>
            new SourceNodeToElementNavAdapter(node);
#pragma warning restore 612,618

        [Obsolete("WARNING! For internal API use only. Turning an untyped SourceNode into a typed ElementNode without providing" +
    "type information (see other overload) will cause side-effects with components in the API that are not prepared to deal with" +
    "missing type information. Please don't use this overload unless you know what you are doing.")]
        public static ITypedElement ToElementNode(this ISourceNode sourceNav) =>
                new SourceNodeToElementNodeAdapter(sourceNav);

    }
}