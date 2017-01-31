using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Resin.Analysis;

namespace Resin.IO.Read
{
    public class LcrsTreeReader : IDisposable
    {
        private readonly StreamReader _sr;
        private LcrsNode _lastRead;
        private LcrsNode _replay;

        public LcrsTreeReader(StreamReader sr)
        {
            _sr = sr;
        }

        private LcrsNode Step()
        {
            if (_replay != null)
            {
                var replayed = _replay;
                _replay = null;
                return replayed;
            }
            var data = _sr.ReadLine();
            
            if (data == null)
            {
                return null;
            }
            _lastRead = new LcrsNode(data);
            return _lastRead;
        }

        private void Replay()
        {
            _replay = _lastRead;
        }

        public bool HasWord(string word)
        {
            if (string.IsNullOrWhiteSpace(word)) throw new ArgumentException("path");

            LcrsNode node;
            if (TryFindDepthFirst(word, 0, out node))
            {
                return node.EndOfWord;
            }
            return false;
        }

        public IList<string> StartsWith(string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix)) throw new ArgumentException("path");

            var compressed = new List<string>();
            LcrsNode node;

            if (TryFindDepthFirst(prefix, 0, out node))
            {
                DepthFirst(prefix, new List<char>(), compressed, prefix.Length-1);
            }

            return compressed;
        }

        public IList<string> Near(string word, int edits)
        {
            var compressed = new List<Word>();
            WithinEditDistanceDepthFirst(word, new string(new char[word.Length]), compressed, 0, edits);
            return compressed.OrderBy(w => w.Distance).Select(w => w.Value).ToList();
        }

        private void WithinEditDistanceDepthFirst(string word, string state, IList<Word> compressed, int depth, int maxEdits)
        {
            var node = Step();
            var nodesWithUnresolvedSiblings = new Stack<Tuple<int, string>>();
            var childIndex = depth + 1;

            // Go left (deep)
            if (node != null)
            {
                string test;
                if (depth == state.Length)
                {
                    test = state + node.Value;
                }
                else
                {
                    test = new string(state.ReplaceOrAppend(depth, node.Value).Where(c => c != Char.MinValue).ToArray());
                }

                var edits = Levenshtein.Distance(word, test);

                if (edits <= maxEdits && node.EndOfWord)
                {
                    compressed.Add(new Word { Value = test, Distance = edits });
                }

                if (node.HaveSibling)
                {
                    nodesWithUnresolvedSiblings.Push(new Tuple<int, string>(depth, string.Copy(state)));
                }

                if (node.HaveChild)
                {
                    WithinEditDistanceDepthFirst(word, string.Copy(test), compressed, childIndex, maxEdits);
                }

                // Go right (wide)
                foreach (var siblingState in nodesWithUnresolvedSiblings)
                {
                    WithinEditDistanceDepthFirst(word, siblingState.Item2, compressed, siblingState.Item1, maxEdits);
                }
            }
        }

        private void DepthFirst(string prefix, IList<char> path, IList<string> compressed, int depth)
        {
            var node = Step();
            var siblings = new Stack<Tuple<int,IList<char>>>();

            // Go left (deep)
            while (node != null && node.Depth > depth)
            {
                var copyOfPath = new List<char>(path);

                path.Add(node.Value);

                if (node.EndOfWord)
                {
                    compressed.Add(prefix + new string(path.ToArray()));
                }

                if (node.HaveSibling)
                {
                    siblings.Push(new Tuple<int, IList<char>>(depth, copyOfPath));
                }

                depth = node.Depth;
                node = Step();
            }

            Replay();

            // Go right (wide)
            foreach (var siblingState in siblings)
            {
                DepthFirst(prefix, siblingState.Item2, compressed, siblingState.Item1);
            }
        }
        
        private bool TryFindDepthFirst(string path, int currentDepth, out LcrsNode node)
        {
            node = Step();

            while (node != null && node.Depth != currentDepth)
            {
                node = Step();
            }

            if (node != null)
            {
                if (node.Value == path[currentDepth])
                {
                    if (currentDepth == path.Length-1)
                    {
                        return true;
                    }
                    // Go left (deep)
                    return TryFindDepthFirst(path, currentDepth+1, out node);
                }
                // Go right (wide)
                return TryFindDepthFirst(path, currentDepth, out node); 
            }

            return false;
        }

        public void Dispose()
        {
            if (_sr != null)
            {
                _sr.Dispose();
            }
        }
    }
}