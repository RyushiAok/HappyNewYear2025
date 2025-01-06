# Snake

<div align="center">
  <img src="https://github.com/user-attachments/assets/d0ac4865-04af-48b6-8b49-829c24247c6e" width=85% />
</div>


## memo

### 盤面表現

- 外枠のセルの値は1で固定
- 枠内のセルの値は残存時間を表し，Snakeの移動（画面更新）に伴い以下のルールに従い更新される
  - 卵を取得できなかった場合，残存時間を1減らす．ただし最小は0
  - 移動先の残存時間にSnakeの長さを加える

```fs
let obstacles =
    if ate then
        state.cells.obstacles + snake.length * head
    else
        torch.max (state.cells.obstacles - (1 - state.cells.border), 0) + snake.length * head
```

| <img src="https://github.com/user-attachments/assets/67731f0e-71a9-4983-9872-ee8dcaa92950" width=75% /> |  <img src="https://github.com/user-attachments/assets/f5fda7ee-b0b6-4a95-b48c-b28769b15d47" width=75% />  |
| :--: | :--: |
| **外枠に衝突** | **Snake自身に衝突** |


### 終了判定
- 「Snakeの先頭より残存時間が長いセルが存在するかどうか」で終了判定する
```fs
let isOver =
    (torch.greater (obstacles, snake.length)).sum().ToScalar().ToInt32() > 0
```

### 状態管理
- MailboxProcessorによりアクターモデルとしてゲームの状態管理を実装する
```fs
let processor =
    MailboxProcessor<Message>.Start(fun inbox ->
        let rec loop state =
            async {
                let! command = inbox.Receive()

                let state =
                    match command with
                    | Action command -> update command state
```
