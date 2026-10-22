using System;
using System.Collections.Generic;
using System.Linq;

namespace SourceGit.Models
{
    public class DecoratorGroup
    {
        public Decorator Decorator { get; set; } = null;
        public List<string> Remotes { get; } = [];

        public bool IsLocal => Decorator.Type is DecoratorType.CurrentBranchHead or DecoratorType.LocalBranchHead or DecoratorType.CurrentCommitHead;
        public bool IsRemote => Decorator.Type == DecoratorType.RemoteBranchHead || Remotes.Count > 0;
        public bool IsHead => Decorator.Type is DecoratorType.CurrentBranchHead or DecoratorType.CurrentCommitHead;

        public static List<DecoratorGroup> Build(List<Decorator> decorators, bool showTags, bool compact)
        {
            var groups = new List<DecoratorGroup>();
            if (decorators == null || decorators.Count == 0)
                return groups;

            var count = decorators.Count;
            var skipped = new HashSet<int>();
            var remotesOf = new Dictionary<int, List<string>>();

            // Pass 1: find remotes to merge into local branches (scan all entries, so a remote
            // listed before its local still merges).
            for (var i = 0; i < count && compact; i++)
            {
                var decorator = decorators[i];
                if (decorator.Type is not (DecoratorType.CurrentBranchHead or DecoratorType.LocalBranchHead))
                    continue;

                for (var j = 0; j < count; j++)
                {
                    var test = decorators[j];
                    if (j == i || test.Type != DecoratorType.RemoteBranchHead)
                        continue;

                    var idxOfSlash = test.Name.IndexOf('/');
                    if (idxOfSlash < 1 || idxOfSlash == test.Name.Length - 1)
                        continue;

                    if (decorator.Name.Equals(test.Name.Substring(idxOfSlash + 1), StringComparison.Ordinal))
                    {
                        if (!remotesOf.TryGetValue(i, out var list))
                            remotesOf[i] = list = [];
                        list.Add(test.Name.Substring(0, idxOfSlash));
                        skipped.Add(j);
                    }
                }
            }

            // Pass 2: emit groups.
            for (var i = 0; i < count; i++)
            {
                if (skipped.Contains(i))
                    continue;

                var decorator = decorators[i];
                if (!showTags && decorator.Type == DecoratorType.Tag)
                    continue;

                var group = new DecoratorGroup() { Decorator = decorator };
                groups.Add(group);

                if (remotesOf.TryGetValue(i, out var remotes))
                    group.Remotes.AddRange(remotes);
            }

            // OrderBy is stable: head -> local -> remote -> tag, git's order kept within a bucket.
            return groups.OrderBy(g => Rank(g.Decorator.Type)).ToList();
        }

        private static int Rank(DecoratorType type)
        {
            return type switch
            {
                DecoratorType.CurrentBranchHead => 0,
                DecoratorType.CurrentCommitHead => 0,
                DecoratorType.LocalBranchHead => 1,
                DecoratorType.RemoteBranchHead => 2,
                DecoratorType.Tag => 3,
                _ => 4,
            };
        }
    }
}
