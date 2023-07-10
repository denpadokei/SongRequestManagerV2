using SongRequestManagerV2.Bots;
using SongRequestManagerV2.SimpleJsons;
using System.Threading;
using Zenject;

namespace SongRequestManagerV2.Models
{
    public class SongInfomationProvider : IInitializable
    {
        public static JSONObject CurrentSongLevel { get; private set; }
        private readonly GameplayCoreSceneSetupData _gameplayCoreSceneSetupData;
        private const int HASH_LENGTH = 40;

        [Inject]
        public SongInfomationProvider(GameplayCoreSceneSetupData gameCoreSceneSetupData)
        {
            this._gameplayCoreSceneSetupData = gameCoreSceneSetupData;
        }

        public async void Initialize()
        {
            CurrentSongLevel = null;
            var level = this._gameplayCoreSceneSetupData.difficultyBeatmap.level;
            var tmp = level.levelID.Split('_');
            if (tmp.Length != 3 || tmp[2].Length < HASH_LENGTH) {
                // 公式譜面とか
                return;
            }
            var result = await WebClient.GetAsync($@"{RequestBot.BEATMAPS_API_ROOT_URL}/maps/hash/{tmp[2].ToUpper().Substring(0, HASH_LENGTH)}", CancellationToken.None).ConfigureAwait(false);
            if (result == null || !result.IsSuccessStatusCode) {
                return;
            }
            CurrentSongLevel = result.ConvertToJsonNode().AsObject;
        }
    }
}
