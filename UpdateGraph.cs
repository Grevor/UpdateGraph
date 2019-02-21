using System;
using System.Collections.Generic;
using System.Linq;

namespace Updater
{
    public class UpdateGraph<TNodeKey, TUpdateKey>
    {
        private List<Node> Nodes { get; } = new List<Node>();

        public void AddUpdate(UpdateAction<TUpdateKey> action, params TUpdateKey[] updates)
        {
            if (Nodes.Any(n => n.Action == action))
                throw new ArgumentException("The specified action is already added.");
            Nodes.Add(new Node(updates, action));
        }

        public void Update(params TUpdateKey[] updates)
        {
            var initialNodes = GetNodesAffectedByUpdates(updates).ToList();

            var nodes = new List<Node>();
            foreach (var node in initialNodes)
            {
                if (node.Mark != Mark.NoMark)
                    continue;

                if (!Visit(node, nodes))
                    HandleDetectedCycle(initialNodes);
            }
            // --- Because out list is in reversed order
            nodes.Reverse();
            // --- Enact updates in order
            foreach (var node in nodes)
                node.Action.Updater?.Invoke();
        }

        private void HandleDetectedCycle(List<Node> initialNodes)
        {
            var V = Nodes.Select(n => new CycleFind(n)).ToDictionary(c => c.Node);
            var stack = new Stack<CycleFind>();
            var output = new List<IEnumerable<CycleFind>>();
            var index = 0;

            foreach (var v in initialNodes.Select(a => V[a]))
            {
                if (v.IndexIsUndefined)
                    StrongConnect(v, V, stack, output, ref index);
            }
            output = output.Where(scc => scc.Count() > 1).ToList();
            throw new InvalidOperationException("The given updates will cause a cyclic update. Cycles are in output.");
        }

        private void StrongConnect(CycleFind v, Dictionary<Node, CycleFind> V, Stack<CycleFind> S, List<IEnumerable<CycleFind>> output, ref int index)
        {
            v.Index = index;
            v.LowLink = index;
            index++;
            S.Push(v);
            v.OnStack = true;

            foreach (var w in GetNodes(v, V))
            {
                if (w.IndexIsUndefined)
                {
                    StrongConnect(w, V, S, output, ref index);
                    v.LowLink = Math.Min(v.LowLink, w.LowLink);
                }
                else if (w.OnStack)
                {
                    v.LowLink = Math.Min(v.LowLink, w.Index);
                }
            }

            if (v.LowLink == v.Index)
            {
                var scc = new List<CycleFind>();
                CycleFind sccNode;
                do
                {
                    sccNode = S.Pop();
                    sccNode.OnStack = false;
                    scc.Add(sccNode);
                } while (sccNode != v);
                output.Add(scc);
            }
        }

        private IEnumerable<CycleFind> GetNodes(CycleFind c, Dictionary<Node, CycleFind> V) => GetNodesAffectedByUpdates(c.Node.TriggeredUpdates).Select(a => V[a]);
        private IEnumerable<Node> GetNodesAffectedByUpdates(IEnumerable<TUpdateKey> updates) => Nodes.Where(n => updates.Any(up => n.IsAffectedByUpdate(up)));

        private bool Visit(Node n, List<Node> nodes)
        {
            if (n.Mark == Mark.Permanent) return true;
            if (n.Mark == Mark.Temporary) return false;

            n.Mark = Mark.Temporary;
            foreach (var m in GetNodesAffectedByUpdates(n.TriggeredUpdates))
                if (Visit(m, nodes))
                    return false; // Propagate cyclic errors

            n.Mark = Mark.Permanent;
            nodes.Add(n);
            return true;
        }

        private enum Mark
        {
            NoMark,
            Temporary,
            Permanent
        }

        private class Node
        {
            public IEnumerable<TUpdateKey> TriggeredUpdates => Action.TriggeredUpdates;
            public UpdateAction<TUpdateKey> Action { get; }
            public HashSet<TUpdateKey> Keys { get; }
            public Mark Mark { get; set; }

            public Node(IEnumerable<TUpdateKey> keys, UpdateAction<TUpdateKey> update)
            {
                Keys = new HashSet<TUpdateKey>(keys);
                Action = update;
            }

            public bool IsAffectedByUpdate(TUpdateKey update) => Keys.Contains(update);
        }

        private class CycleFind
        {
            public CycleFind(Node n) => Node = n;

            public int LowLink { get; set; } = -1;
            public int Index { get; set; } = -1;
            public bool OnStack { get; set; } = false;
            public Node Node { get; }

            public bool IndexIsUndefined => Index < 0;
        }
    }
}
