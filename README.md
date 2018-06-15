# energy-balance-solver
An windows form to solve the puzzles from the Energy Balance game.

A compiled version is available in the releases link above, otherwise compile from source for yourself using Visual Studio Community 2017.

## Requirements To Run

- A windows computer
- .NET 4.5 Installed

## Directions

1) Fill out the grid with the numbers from the game screen
2) Shift-Click the cells that are the "answers" they will highlight in green.
3) If an answer cell touches more than one other non-answer cell, you must provided the direction from the answer cell to the row/column that the answer is for by way of appending L, R, U, D (in reality I think they will always be L or U).
4) Click Solve (the program should produce an answer pretty quickly!, your mileage may vary based on how powerful your computer is)

## Example

[[https://github.com/akrock/energy-balance-solver/blob/master/test-puzzle.jpg|alt=test_puzzle]]

And a real-time demo of the solving!

[[https://github.com/akrock/energy-balance-solver/blob/master/test-puzzle.gif|alt=test_solution]]
