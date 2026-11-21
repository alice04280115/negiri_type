# ネギリタイプ

タイピングがうまいほど買い物が安くなる、ひと月のやりくりゲーム（unityroom 向け WebGL）。

## いまの段階：段階1（タイピングだけの画面）

商品名を打つと、残り時間と正確率から評価（PERFECT〜MISS）が出て、値引き後の価格が表示されます。
確認したいのは「制限時間と評価の境目がちょうどよいか」「値切れて嬉しいか」です。

### はじめ方

1. Unity Hub で **Unity 6 LTS** をインストールし、「Add project from disk」でこのフォルダーを開く。
2. 初回に Unity が `ProjectSettings/` と各ファイルの `.meta` を作るので、それらもコミットする。
3. 空のシーンのまま再生（▶）すると、段階1の画面が自動で出る。

### 操作

| キー | 動作 |
| --- | --- |
| スペース | 開始 |
| 1 / 2 / 3 / 4 | 難易度（初級・中級・上級・達人）の切り替え（開始前のみ） |
| R | 読みがなを出す・隠す（上級・達人のみ、開始前） |
| Esc | その商品を飛ばす |
| Enter | 次の商品へ |

日本語入力がオンのまま打つと注意が出るので、半角英数に切り替えてください。

### 日本語フォントについて

WebGL ビルドでは日本語フォントを同梱する必要があります。
`Assets/Resources/Fonts/main.ttf`（またはotf）にフリーの日本語フォントを置くと、それが使われます（未定）。

## フォルダー構成

- `Assets/Scripts/Core` … ローマ字判定・評価・価格計算など。Unity に依存しない C#。
- `Assets/Scripts/Game` … 画面と進行（MonoBehaviour）。
- `Assets/Resources/Data` … 商品・文章・調整値の JSON。
- `Assets/Tests/EditMode` … Core のテスト。
- `tools/CoreTests` … 同じテストを Unity なしで動かす .NET プロジェクト。

## テスト

- Unity：Window > General > Test Runner の EditMode。
- Unity なし：`dotnet test tools/CoreTests`（.NET 8 SDK が必要）。GitHub Actions でも自動実行。
