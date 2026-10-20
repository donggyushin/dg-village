using UnityEngine;

namespace DgVillage
{
    /// <summary>
    /// 채널 (서버 api.py 의 /channels). 채널 목록 씬에서 고른 채널을 Current 에 기억해 두고,
    /// 마을 씬의 NetworkClient 가 그 id 로 join 한다. static 이라 씬이 바뀌어도 남는다.
    /// 마을 씬을 에디터에서 바로 Play 하면 아무것도 안 골랐으니 기본 채널(광장)로 들어간다.
    /// </summary>
    public static class ChannelClient
    {
        /// <summary>서버 DEFAULT_CHANNEL_ID (광장) 와 같게 유지</summary>
        public const string DefaultChannelId = "00000000000000000000000000000001";

        /// <summary>서버 CHANNEL_NAME_MAX_LENGTH 와 같게 유지</summary>
        public const int NameMaxLength = 24;

        /// <summary>서버 MAX_CHANNELS_PER_USER 와 같게 유지 (안내 문구용)</summary>
        public const int MaxChannelsPerUser = 3;

        /// <summary>들어갈(들어가 있는) 채널. 아직 안 골랐으면 null.</summary>
        public static ChannelInfo Current { get; private set; }

        public static string CurrentId => Current != null ? Current.id : DefaultChannelId;

        public static void Select(ChannelInfo channel) => Current = channel;

        /// <summary>광장이 맨 앞, 나머지는 사람 많은 순 (서버가 정렬해서 보낸다).</summary>
        public static async Awaitable<ChannelInfo[]> ListAsync()
        {
            var list = await AccountClient.SendAsync<ChannelList>("GET", "/channels", null);
            return list.channels ?? new ChannelInfo[0];
        }

        /// <summary>새 채널을 만든다 (주인 = 나). 이미 MaxChannelsPerUser 개 있으면 409.</summary>
        public static async Awaitable<ChannelInfo> CreateAsync(string name)
        {
            return await AccountClient.SendAsync<ChannelInfo>("POST", "/channels", new ChannelCreate { name = name });
        }

        public static string NormalizeName(string value) => AccountClient.NormalizeName(value, NameMaxLength);
    }
}
