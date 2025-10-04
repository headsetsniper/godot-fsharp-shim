namespace Game

open System

[<System.Flags>]
type CellFlags =
    | Empty = 0
    | Filled = 1

module Tetromino =
    type Kind =
        | I
        | O
        | T
        | S
        | Z
        | J
        | L

    let all = [| Kind.I; Kind.O; Kind.T; Kind.S; Kind.Z; Kind.J; Kind.L |]

    let shape (k: Kind) : bool[,] =
        match k with
        | Kind.O -> array2D [ [ true; true ]; [ true; true ] ]
        | Kind.I -> array2D [ [ true; true; true; true ] ]
        | Kind.T -> array2D [ [ true; true; true ]; [ false; true; false ] ]
        | Kind.S -> array2D [ [ false; true; true ]; [ true; true; false ] ]
        | Kind.Z -> array2D [ [ true; true; false ]; [ false; true; true ] ]
        | Kind.J -> array2D [ [ true; false; false ]; [ true; true; true ] ]
        | Kind.L -> array2D [ [ false; false; true ]; [ true; true; true ] ]

    let rotateCW (s: bool[,]) : bool[,] =
        let h = s.GetLength 0
        let w = s.GetLength 1
        let r = Array2D.zeroCreate w h

        for y in 0 .. h - 1 do
            for x in 0 .. w - 1 do
                r[x, h - 1 - y] <- s[y, x]

        r
