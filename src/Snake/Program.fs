open System
open TorchSharp

type Direction =
    | Up
    | Down
    | Left
    | Right


type Snake = {
    x: int
    y: int
    length: int
    direction: Direction
}

module Snake =
    let init (x: int, y: int) : Snake = {
        x = x
        y = y
        length = 1
        direction = Right
    }

    let move (direction: Direction) (state: Snake) : Snake =

        let (dx, dy): int * int =
            direction
            |> function
                | Up -> (0, 1)
                | Down -> (0, -1)
                | Left -> (-1, 0)
                | Right -> (1, 0)

        {
            state with
                x = state.x + dx
                y = state.y + dy
                direction = direction
        }


type Cells = {
    width: int
    height: int
    border: torch.Tensor
    obstacles: torch.Tensor
    snakeHead: torch.Tensor
    egg: torch.Tensor
}

module Cells =
    let border (height: int, width: int) : torch.Tensor =
        torch.from_array (
            Array2D.init height width (fun y x ->
                if x = 0 || x = width - 1 || y = 0 || y = height - 1 then
                    1
                else
                    0)
        )

    let snakeHead (snake: Snake, height: int, width: int) =
        let head = torch.zeros ([| int64 height; int64 width |], dtype = torch.int32)
        head[snake.y, snake.x] <- 1
        head

    let egg (height: int, width: int, obstacles, snakeHead) =
        let placeable = torch.eq (torch.bitwise_or (obstacles, snakeHead), 0)
        let indices = torch.nonzero (placeable)
        let sample = indices[torch.randperm(indices.size (0))[0]]
        let egg = torch.zeros ([| int64 height; int64 width |], dtype = torch.int32)
        egg[sample[0], sample[1]] <- 1
        egg


    let init (height: int, width: int) : Cells =
        let obstacles = border (height, width)
        let snakeHead = snakeHead (Snake.init (width / 2, height / 2), height, width)

        {
            width = width
            height = height
            border = obstacles
            obstacles = obstacles + snakeHead
            snakeHead = snakeHead
            egg = egg (height, width, obstacles, snakeHead)
        }


module Game =
    type State = {
        cells: Cells
        lastUpdated: DateTime
        snake: Snake
        isOver: bool
    }

    type Message =
        | Action of Direction
        | Step


    let init (height: int, width: int) : State =
        let snake = Snake.init (width / 2, height / 2)

        {
            cells = Cells.init (height, width)
            snake = snake
            isOver = false
            lastUpdated = DateTime.Now
        }

    let update (dir: Direction) (state: State) : State =
        let snake = Snake.move dir state.snake
        let head = Cells.snakeHead (snake, state.cells.height, state.cells.width)

        let ate =
            (torch.bitwise_and (head, state.cells.egg)).sum().ToScalar().ToInt32() > 0

        let snake = {
            snake with
                length = snake.length + (if ate then 1 else 0)
        }

        let obstacles =
            if ate then
                state.cells.obstacles + snake.length * head
            else
                torch.max (state.cells.obstacles - (1 - state.cells.border), 0)
                + snake.length * head

        let isOver =
            (torch.greater (obstacles, snake.length)).sum().ToScalar().ToInt32() > 0

        let egg =
            if ate then
                Cells.egg (state.cells.height, state.cells.width, obstacles, head)
            else
                state.cells.egg

        {
            cells = {
                state.cells with
                    snakeHead = head
                    obstacles = obstacles
                    egg = egg
            }
            snake = snake
            isOver = isOver
            lastUpdated = DateTime.Now
        }

    let render (state: State) : unit =
        Console.Clear()

        let stringMap =
            (state.cells.obstacles - state.cells.egg).data<int>().ToArray()
            |> Array.map (function
                | -1 -> "🥚 "
                | 0 -> "   "
                | 2 -> "\u001b[47;30m H \u001b[0m"
                | 3 -> "\u001b[47;30m a \u001b[0m"
                | 4 -> "\u001b[47;30m p \u001b[0m"
                | 5 -> "\u001b[47;30m p \u001b[0m"
                | 6 -> "\u001b[47;30m y \u001b[0m"
                | 7 -> "\u001b[47;30m   \u001b[0m"
                | 8 -> "\u001b[47;30m N \u001b[0m"
                | 9 -> "\u001b[47;30m e \u001b[0m"
                | 10 -> "\u001b[47;30m w \u001b[0m"
                | 11 -> "\u001b[47;30m   \u001b[0m"
                | 12 -> "\u001b[47;30m Y \u001b[0m"
                | 13 -> "\u001b[47;30m e \u001b[0m"
                | 14 -> "\u001b[47;30m a \u001b[0m"
                | 15 -> "\u001b[47;30m r \u001b[0m"
                | _ -> "\u001b[47m   \u001b[0m")

        stringMap
        |> Array.chunkBySize (state.cells.width)
        |> Array.map (String.concat "")
        |> String.concat "\n"
        |> printfn "%s"


    let processor =
        MailboxProcessor<Message>.Start(fun inbox ->
            let rec loop state =
                async {
                    let! command = inbox.Receive()

                    let state =
                        match command with
                        | Action command -> update command state
                        | Step ->
                            let elapsed = DateTime.Now - state.lastUpdated

                            if elapsed.TotalMilliseconds > 1000.0 then
                                update state.snake.direction state
                            else
                                state

                    render state

                    if state.isOver then
                        printfn "Game Over!"
                    else
                        return! loop state
                }

            loop (init (10, 20)))


let timer =
    async {
        while true do
            do! Async.Sleep(100)
            Game.processor.Post(Game.Step)
    }

let userInput =
    async {
        while true do
            let! input =
                Async.FromContinuations(fun (cont, _, _) ->
                    let key = Console.ReadKey().KeyChar
                    cont key)

            let command =
                match input with
                | 'w' -> Some Down
                | 's' -> Some Up
                | 'a' -> Some Left
                | 'd' -> Some Right
                | _ -> None

            match command with
            | Some command -> Game.processor.Post(Game.Action command)
            | None -> ()
    }

Async.Parallel [ timer; userInput ]
|> Async.RunSynchronously
|> ignore
