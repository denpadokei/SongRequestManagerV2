# Song Request Manager V2

[![Build and Release](https://github.com/denpadokei/SongRequestManagerV2/actions/workflows/build-and-release.yml/badge.svg)](https://github.com/denpadokei/SongRequestManagerV2/actions/workflows/build-and-release.yml)

Twitch のチャットから曲のリクエストを受け付け、ゲーム内でキュー管理・検索・ダウンロード・再生まで行えるリクエストマネージャーです。チャット連携は CatCore を使用し、Streamer.bot 経由でのメッセージ受信にも対応しています。

## 機能

- `!bsr`（`!request` / `!add` / `!srm`）で BeatSaver の検索結果や ID からリクエストをキューに追加
- 曲選択画面の右上に SRM ボタンを表示し、キュー一覧・履歴・スキップ・ブラックリスト・再生をゲーム内で操作
- 未所持の曲は BeatSaver からダウンロードし、`CustomLevels`（WIP は `CustomWIPLevels`）へ展開して曲リストを更新・スクロール
- 検索用キーボード（CLEAR SEARCH / NEWEST / RANKED / UNFILTERED / SEARCH）を内蔵
- ユーザー・サブスク・VIP・モデレーターごとのリクエスト上限、レーティング／NJS／曲長によるフィルタ
- バンリスト、リマップ、マッパーバンなどのフィルタ管理
- 約 60 種類のチャットコマンド（`!queue` / `!link` / `!who` / `!mtt` / `!att` / `!remove` / `!clearqueue` / `!lookup` など）。`!help` と `!commandlist` で一覧を確認可能
- コマンドの別名・権限・ヘルプを `SRMCommands.ini` でカスタマイズ
- OBS 用に `queuelist.txt` と `queuestatus.txt` を出力し、キュー内容と開閉状態を配信画面へ表示
- 通知音の再生、`!link` のリンク種別切替、パフォーマンスモード
- Streamer.bot の WebSocket からチャットメッセージを受信（Twitch 以外のプラットフォームも指定可能）
- GitHub のリリースを確認し、新しいバージョンがあればゲーム内から更新

## 動作環境

| 項目 | 内容 |
| --- | --- |
| Beat Saber | 1.39.1 |
| 依存 MOD | SongCore 3.14.15 / BSIPA 4.3.5 / CatCore 1.1.0 / SiraUtil 3.1.14 / BeatSaberMarkupLanguage 1.12.3 |

## インストール

1. Beat Saber を 1.39.1 に合わせる (BSManager などのバージョン管理ツールを推奨)
2. BSIPA と上記の依存 MOD をインストールする
3. [Releases](https://github.com/denpadokei/SongRequestManagerV2/releases) から `SongRequestManagerV2-14.0.3-bs1.39.1.zip` をダウンロードし、`Plugins` と `UserData` フォルダをゲームフォルダへ上書きコピーする

Twitch のチャットへ接続するには CatCore が必要です。

## 使い方

### ゲーム内

- 曲選択画面の右上にある SRM ボタン（OPEN / CLOSE）を押すとリクエストパネルが開きます。ボタンの色はキューにリクエストがあると緑になります。
- パネルではキュー一覧の選択、履歴への切替、**Skip**（キューから削除）、**Blacklist**（今後ブロック）、**Play**（ダウンロードして曲リストをスクロール）ができます。
- キーボードから曲名を検索し、**NEWEST** / **RANKED** / **UNFILTERED** で並び順やフィルタを切り替えられます。
- キューの開閉は **CLOSE / OPEN** ボタン、またはモデレーターの `!open` / `!close` で行います。

### チャットコマンド（抜粋）

| コマンド | 権限 | 説明 |
| --- | --- | --- |
| `!bsr <曲名 or ID>` | 全員 | リクエストをキューに追加 |
| `!queue` | 全員 | 現在のキューを表示 |
| `!link` | 全員 | 再生中の曲の情報と BeatSaver リンクを表示 |
| `!wrongsong` | 全員 | 自分の最後のリクエストを取り消す |
| `!lookup <曲名 or ID>` | サブ / VIP / モデレーター | BeatSaver の検索結果を表示 |
| `!open` / `!close` | モデレーター | キューの受付を開く／閉じる |
| `!mtt` / `!att` / `!last` | モデレーター | リクエストを先頭／末尾へ移動 |
| `!remove` | モデレーター | リクエストを削除 |
| `!block` / `!unblock` | モデレーター | 曲 ID のブロック／解除 |
| `!clearqueue` | モデレーター | キューを空にする |
| `!help` / `!commandlist` | 全員 | コマンドのヘルプ／一覧 |

コマンドの別名・権限・ヘルプは `UserData/Song Request ManagerV2/SRMCommands.ini` で変更できます。

### Streamer.bot

設定で `EnableStreamerBot` を有効にすると、`StreamerBotWebSocketPort`（既定 8080）と `StreamerBotWebSocketEndpoint`（既定 `/`）で Streamer.bot に接続します。`SendChatActionGUID` / `SendChatActionName` に送信用アクションを指定すると、Bot のメッセージを Streamer.bot 経由で送信します。

### OBS 連携

`UpdateQueueStatusFiles` が有効な場合、`UserData/Song Request ManagerV2/queuelist.txt` と `queuestatus.txt` が更新されます。OBS のテキストソース（GDI+）で読み込むと、キューと受付状態を配信画面に表示できます。`MaximumQueueTextEntries` を超えた分は `...` になります。

## 設定

設定は BSIPA により `UserData` 配下の JSON に保存され、ゲーム内の Mod Settings → **SRM V2** から変更できます。`SRMCommands.ini` は `UserData/Song Request ManagerV2` にあります。

| 項目 | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| AutoOpenRequestQueue | bool | true | 起動時にキューを開く |
| RequestQueueOpen | bool | true | キューの受付状態 |
| ClearNoFail | bool | false | リクエスト曲を選んだとき NoFail を解除する |
| RequestHistoryLimit | int | 40 | 履歴に残す件数 |
| UserRequestLimit | int | 2 | 一般ユーザーの同時リクエスト上限 |
| SubRequestLimit | int | 5 | サブスクの同時リクエスト上限 |
| ModRequestLimit | int | 10 | モデレーターの同時リクエスト上限 |
| VipBonusRequests | int | 1 | VIP に加算するリクエスト数 |
| SessionResetAfterXHours | int | 6 | セッション情報をリセットするまでの時間 |
| LimitUserRequestsToSession | bool | false | 曲を再生してもリクエスト上限をリセットしない |
| LowestAllowedRating | float | 0 | 許可する最低レーティング（0〜100） |
| MaximumSongLength | float | 180 | 許可する最大曲長（分） |
| MinimumNJS | float | 0 | 許可する最低 NJS |
| MaxiumScanRange | int | 5 | 新着曲を探す範囲 |
| AutopickFirstSong | bool | false | 検索結果の最初の曲を自動で選ぶ |
| UpdateQueueStatusFiles | bool | true | `queuelist.txt` / `queuestatus.txt` を出力する |
| MaximumQueueTextEntries | int | 8 | テキストファイルに出すキューの件数 |
| MaximumQueueMessages | int | 1 | `!queue` などで送るメッセージ数 |
| BotPrefix | string | "" | Bot の全出力に付ける接頭辞（TTS 用。`"! "` で TTS フィルタしやすくなる） |
| ModFullRights | bool | false | モデレーターに配信者と同等の権限を与える |
| PPSearch | bool | true | PP 順の検索を有効にする |
| PerformanceMode | bool | false | パフォーマンス優先の動作にする |
| NotifySound | bool | false | リクエスト通知音を鳴らす |
| SoundVolume | int | 50 | 通知音の音量（0〜100） |
| LinkType | enum | All | `!link` の対象（`OnlyRequest` / `All`） |
| EnableStreamerBot | bool | false | Streamer.bot の WebSocket を使用する |
| StreamerBotWebSocketPort | int | 8080 | Streamer.bot の WebSocket ポート |
| StreamerBotWebSocketEndpoint | string | "/" | Streamer.bot の WebSocket エンドポイント |
| SendChatActionGUID | string | "" | メッセージ送信に使う Streamer.bot アクションの GUID |
| SendChatActionName | string | "" | メッセージ送信に使う Streamer.bot アクション名 |
| UsePlatform | list | Twitch | Streamer.bot で受信するプラットフォーム |
| EnableAprilFool | bool | false | エイプリルフール演出を有効にする |

## ビルド

```powershell
git clone https://github.com/denpadokei/SongRequestManagerV2.git
cd SongRequestManagerV2
dotnet build .\SongRequestManagerV2\SongRequestManagerV2.csproj -c Release
```

`BeatSaberDir` に Beat Saber のインストール先を設定すると、ゲームの参照アセンブリを解決できます。

GitHub Actions の `build-and-release.yml` が push ごとにビルドし、`X.X.X` 形式のタグを push するとリリースを作成します。

## ライセンス

GNU Lesser General Public License v3.0（`LICENSE.md`、日本語参考訳 `LICENSE JP.md`）。

本 MOD は angturil 氏の [SongRequestManager](https://github.com/angturil/SongRequestManager) を元にしています。© 2018 angturil
