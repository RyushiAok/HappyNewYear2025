# Snake
![Snake2025](https://github.com/user-attachments/assets/d0ac4865-04af-48b6-8b49-829c24247c6e)

## memo

- 「Snakeの先頭より残存時間が長いセルが存在するかどうか」で終了判定する
  -  ただし，外枠の残存時間は1
```fs
let isOver =
    (torch.greater (obstacles, snake.length)).sum().ToScalar().ToInt32() > 0
```

- MailboxProcessorによりアクターモデルとしてゲームの状態管理を実装する
