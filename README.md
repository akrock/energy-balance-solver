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

![Test_Solution](https://raw.githubusercontent.com/akrock/energy-balance-solver/master/test-puzzle.jpg)

And a real-time demo of the solving!

![Test_Solution](https://raw.githubusercontent.com/akrock/energy-balance-solver/master/test-solve.gif)

## License

Copyright 2018 

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.