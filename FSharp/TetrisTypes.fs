namespace Game

open System

[<Flags>]
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

    let all = [| I; O; T; S; Z; J; L |]

    let shape (k: Kind) : bool[,] =
        match k with
        | O -> array2D [ [ true; true ]; [ true; true ] ]
        | I -> array2D [ [ true; true; true; true ] ]
        | T -> array2D [ [ true; true; true ]; [ false; true; false ] ]
        | S -> array2D [ [ false; true; true ]; [ true; true; false ] ]
        | Z -> array2D [ [ true; true; false ]; [ false; true; true ] ]
        | J -> array2D [ [ true; false; false ]; [ true; true; true ] ]
        | L -> array2D [ [ false; false; true ]; [ true; true; true ] ]

    let rotateCW (s: bool[,]) : bool[,] =
        let h = s.GetLength 0
        let w = s.GetLength 1
        let r = Array2D.zeroCreate w h

        for y in 0 .. h - 1 do
            for x in 0 .. w - 1 do
                r[x, h - 1 - y] <- s[y, x]

        r
