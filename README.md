# PodcastUploader

Spotify for Creators 向けのポッドキャストアップロード補助ツールです。Windows Forms と Playwright を使って、エピソード作成画面への入力を自動化します。

## できること

- 音声ファイルを選択して Spotify for Creators の新規エピソード画面へアップロードする
- MP3 タグのタイトルからエピソード番号を抽出する
- episode.json に保存した説明文を自動入力する
- シーズン番号 1 とエピソード番号を自動設定する
- 公開設定画面の直前または公開設定画面まで進める

## 動作前提

- Windows
- .NET 10 SDK
- Spotify for Creators にアクセスできるアカウント
- 初回実行時または事前に Playwright 用ブラウザが利用可能であること

使用している主なパッケージ:

- Microsoft.Playwright 1.59.0
- TagLibSharp 2.3.0

## ファイル構成

- PodcastUploader.cs: アプリ本体
- PodcastUploader.csproj: プロジェクト設定
- episode.json: 説明文や音声ファイルパスの設定
- bin/Debug/net10.0-windows/browser_data: Playwright の永続ブラウザープロファイル

## episode.json

アプリは次の優先順で episode.json を読み込みます。

- 開発環境: PodcastUploader.csproj が見つかるプロジェクトフォルダー配下の episode.json
- 実行環境: 実行フォルダー配下の episode.json

そのため、dotnet run で起動したときはプロジェクト直下の episode.json が更新され、配布した実行環境では実行フォルダー側の episode.json が更新されます。最低限、次のような形式で利用できます。

```json
{
  "EpisodeNumber": 0,
  "Title": "",
  "Description": "<p>ここに説明文を入れます</p>",
  "AudioFilePath": "D:\\Podcast\\episode.mp3"
}
```

各項目の用途:

- EpisodeNumber: 現状は自動入力では未使用
- Title: 現状は MP3 タグのタイトルを優先するため未使用
- Description: Spotify の説明文欄へ入力する内容
- AudioFilePath: 前回利用した音声ファイルパスの保存先

## MP3 タグの前提

音声ファイルのタイトルタグに「第829回」のような形式が含まれている必要があります。

例:

- 第829回 WoodStream のデジタル生活
- Windows Podcast 第100回

この数字を使ってエピソード番号を設定します。

## 使い方

1. 必要なら episode.json の Description を編集する
2. 次のコマンドでビルドする

```powershell
dotnet build .\PodcastUploader.csproj
```

3. 次のコマンドで起動する

```powershell
dotnet run
```

4. アプリ上で音声ファイルを選択する
5. アップロード実行 を押す
6. 起動したブラウザで Spotify for Creators にログイン済みであることを確認する
7. 自動入力後、ブラウザ上で内容を確認して必要に応じて公開する

## 初回利用時の注意

- Playwright のブラウザプロファイルは browser_data に保存されます
- 一度ログインすると、次回以降は同じプロファイルを再利用します
- ログイン状態や画面状態が不安定な場合は browser_data を見直してください

## 実装上の流れ

アプリは次の順で処理します。

1. 音声ファイルの存在確認
2. episode.json の読み込み
3. MP3 タグからタイトル取得
4. タイトルから「第○回」の数字を抽出
5. Spotify for Creators の対象ページを開く
6. 新しいエピソードを作成する
7. 音声ファイル、タイトル、説明文、シーズン番号、エピソード番号を入力する
8. 公開設定へ進み、「今すぐ」を選択する

## トラブルシューティング

### 説明文が入力されない

Spotify 側の UI 変更で、説明文欄の DOM 構造が変わることがあります。過去に正常動作していたビルドとの差分確認や、実際の説明欄 DOM の確認が有効です。

### 公開日「今すぐ」で止まる

公開設定画面のラベルや属性が変更されると、Playwright のロケータが見つからなくなることがあります。Spotify 側の画面構造変更を確認してください。

### MP3 のタイトルエラーが出る

音声ファイルのタイトルタグに「第○回」が含まれているか確認してください。

### Playwright 関連の参照エラーがエディタに残る

CLI のビルドが通っている場合でも、VS Code 側の診断が残ることがあります。次を順に試してください。

```powershell
dotnet restore
```

その後に VS Code の C# 言語サービスやウィンドウを再読み込みしてください。

## 補足

- このツールは Spotify 側の Web UI に依存しています
- Spotify 側の仕様変更により、突然動かなくなる可能性があります
- 自動処理後の最終確認と公開操作は手動で行ってください
