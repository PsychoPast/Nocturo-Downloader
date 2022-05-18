using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Nocturo.Common.Exceptions;
using Nocturo.Downloader.Enums;

namespace Nocturo.Downloader
{
    public class InstallTagCollection : IReadOnlyDictionary<InstallTagType, HashSet<string>>
    {
        private IReadOnlyDictionary<InstallTagType, HashSet<string>> _dic;

        public HashSet<string> this[InstallTagType key] => _dic[key];

        public IEnumerable<InstallTagType> Keys => _dic.Keys;

        public IEnumerable<HashSet<string>> Values => _dic.Values;

        public int Count => _dic.Count;

        public InstallTagType GetKeyFromSubValue(string installTag)
        {
            foreach (InstallTagType key in _dic.Keys)
            {
                HashSet<string> set = _dic[key];
                if (set.Contains(installTag))
                    return key;
            }

            throw new NotFoundException();
        }

        public bool ContainsKey(InstallTagType key) => _dic.ContainsKey(key);

        public IEnumerator<KeyValuePair<InstallTagType, HashSet<string>>> GetEnumerator() => _dic.GetEnumerator();

        public bool TryGetValue(InstallTagType key, [MaybeNullWhen(false)] out HashSet<string> value) => _dic.TryGetValue(key, out value);

        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_dic).GetEnumerator();
    }
}