﻿/// Based on Tao Liu´s code
/// http://apollo13cn.blogspot.de/2011/04/f-and-genetic-algorithms.html

/// Evolutionary computation module
/// this module provides the Genetic Algorithm (GA)
///
/// this version depends on explicit type parameters;
/// the original version depends on casting to and from obj
/// (similar to C casting to and from void pointer)
module EvolutionaryComputation =
    /// random number generator
    let random = new System.Random((int)System.DateTime.Now.Ticks)

    /// random int generator
    let randomF() = random.Next()

    /// random float (double) generator
    let randomFloatF() = random.NextDouble()

    type unitTo<'T> = unit -> 'T
    type ConvertersType<'T> = ('T -> 'T) list
    type FitnessType<'T,'S> = 'T list -> 'S 

    /// Chromosome type to represent the individuals involved in the GA
    type ChromosomeType<'T,'S when 'S: comparison> (initialF: unitTo<'T>, 
                                                    fitnessF: FitnessType<'T,'S>, 
                                                    toInt: 'T -> int, 
                                                    size, 
                                                    ?converters: ConvertersType<'T>) = 
        let mutable genos = [for i in 1..size do yield initialF()]
        let mutable genoPhenoConverters = converters

        /// make a duplicate copy of this chromosome
        member this.Clone() =
            let newValue = 
                match converters with
                    | Some(converters) -> new ChromosomeType<'T,'S>(initialF, fitnessF, toInt, size, converters)
                    | None -> new ChromosomeType<'T,'S>(initialF, fitnessF, toInt, size)
            newValue.Genos <- this.Genos 
            newValue 

        /// get fitness value with given fitness function
        member this.Fitness() = this.Pheno |> fitnessF

        /// gets and sets the Geno values
        member this.Genos
            with get() = genos 
            and set(value) = genos <- value 

        /// gets and sets the Pheno values
        member this.Pheno
            with get(): 'T list = 
                match genoPhenoConverters with 
                | Some(genoPhenoConverters) -> List.zip genoPhenoConverters genos |> List.map (fun (f,value) -> f value) 
                | None -> this.Genos

        /// mutate the chromosome with given mutation function
        member this.Mutate(?mutationF) =
            let location = random.Next(Seq.length this.Genos)
            let F = 
                match mutationF with
                    | Some(mutationF) -> mutationF
                    | None -> initialF
            let transform i v =
                match i with
                    | _ when i=location -> F()
                    | _ -> v
            this.Genos <- List.mapi transform this.Genos 

        member this.ToRows() = [ this.Genos |> List.map toInt; this.Pheno |> List.map toInt ]

    /// generate a population for GAs
    let Population<'T,'S when 'S: comparison> randomF fitnessF toInt populationSize chromosomeSize = 
        [for i in 1..populationSize do yield (new ChromosomeType<'T,'S>(initialF=randomF,fitnessF=fitnessF,toInt=toInt,size=chromosomeSize))] 

    /// find the maximum fitness value from a population
    let maxFitness<'T,'S when 'S: comparison> population  = 
        let best = Seq.maxBy (fun (c:ChromosomeType<'T,'S>) -> c.Fitness()) population
        best.Fitness()

    /// find the fittest individual
    let bestChromosome<'T,'S when 'S: comparison> population = 
        let best = Seq.maxBy (fun (c:ChromosomeType<'T,'S>) -> c.Fitness()) population
        best

    /// rank selection method
    let RankSelection<'T,'S when 'S: comparison> (population:ChromosomeType<'T,'S> list) = 
        let populationSize = population.Length
        let r() = randomF() % populationSize
        let randomSelection() = 
            let c0 = population.[r()]
            let c1 = population.[r()]
            let result = if (c0.Fitness() > c1.Fitness()) then c0 else c1
            result.Clone();
        Seq.init populationSize (fun _ -> randomSelection())

    /// shuffle crossover
    let ShuffleCrossover<'T,'S when 'S: comparison> (c0:ChromosomeType<'T,'S>) (c1:ChromosomeType<'T,'S>) =
        let crossover c0 c1 =
            let isEven n = n%2 = 0
            let randomSwitch (x,y) = if isEven (randomF()) then (x,y) else (y,x) 
            List.zip c0 c1 |> List.map randomSwitch |> List.unzip
        let (first,second) = crossover (c0.Genos) (c1.Genos)
        c0.Genos <- first 
        c1.Genos <- second
        
    /// evolve the whole population 
    let Evolve<'T,'S when 'S: comparison> ((population: ChromosomeType<'T,'S> list),
                                            selectionF: ChromosomeType<'T,'S> list -> ChromosomeType<'T,'S> seq,
                                            crossoverF, 
                                            crossoverRate, 
                                            mutationRate, 
                                            elitism) = 
        let populationSize = Seq.length population 
        let r() = randomF() % populationSize 
        let elites = selectionF population |> Seq.toList
        let seq0 = elites |> Seq.mapi (fun i element->(i,element)) |> Seq.filter (fun (i,_)->i%2=0) |> Seq.map (fun (_,b)->b)
        let seq1 = elites |> Seq.mapi (fun i element->(i,element)) |> Seq.filter (fun (i,_)->i%2<>0) |> Seq.map (fun (_,b)->b)
        let xoverAndMutate (a:ChromosomeType<'T,'S>) (b:ChromosomeType<'T,'S>) =
            if (randomFloatF() < crossoverRate) then 
                crossoverF a b 
            if (randomFloatF() < mutationRate) then 
                a.Mutate() 
            if (randomFloatF() < mutationRate) then 
                b.Mutate() 
            [a] @ [b] 

        if elitism then
            let seq0 = seq0
            let seq1 = seq1
            let r = Seq.map2 xoverAndMutate seq0 seq1 |> List.concat
            r.Tail @ [  bestChromosome population ]
        else
            Seq.map2 xoverAndMutate seq0 seq1 |> List.concat

    /// composite function X times
    let rec composite f x = 
         match x with
            | 1 -> f
            | n -> f >> (composite f (x-1))

    /// convert a function seq to function composition
    let compositeFunctions functions = 
         Seq.fold ( >> ) id functions

    let populationToMatrix<'T,'S when 'S: comparison> (population: ChromosomeType<'T,'S> list) = 
        [ for i in population do yield (i.ToRows()) ] 
        |> List.concat


/// Generate heat maps as graphical representation for the evolution of chromosomes
module Drawing =
    open System.Drawing
    open System.Windows.Forms

    /// draw a rectangle of given size and color on a given graphics element,
    /// throw System.ArgumentNullException on unvalid graphics element
    let drawRectangle (gr: Graphics) clr x1 y1 x2 y2 =
        use br = new SolidBrush(clr)
        use pen = new Pen(br)
        let left, top = min x1 x2, min y1 y2
        let width, height = abs(x1 - x2), abs(y1 - y2)
        gr.DrawRectangle(pen, Rectangle(left, top, width, height))
        gr.FillRectangle(br, Rectangle(left, top, width, height)) 
    
    /// mapping to a color scheme  of size 10,
    /// cyclic mapping modulo 10
    let defaultColorTable i_ =
        let i = i_ % 10
        match i with
        | 0 -> Color.SpringGreen
        | 1 -> Color.Lime
        | 2 -> Color.Chartreuse
        | 3 -> Color.GreenYellow
        | 4 -> Color.Yellow
        | 5 -> Color.Gold
        | 6 -> Color.Orange
        | 7 -> Color.DarkOrange
        | 8 -> Color.OrangeRed
        | _ -> Color.Red
        
    /// draw a heat map on a given windows form of an integer matrix (list of lists),
    /// using a integer based color mapping and fix sized heat map items
    let heatMap (form:Form) colorTable size (matrix: (int list) list) =
        use gr = form.CreateGraphics()
        let drawElement x i j = 
            let color = colorTable x
            drawRectangle gr color (i*size) (j*size) ((i+1)*size) ((j+1)*size) 
        matrix 
        |> List.iteri (fun i row -> row |> List.iteri (fun j x -> drawElement x i j))
 
    /// save a heat map as jpg file,
    /// generated from an integer matrix (list of lists),
    /// throw System.Exception on failed bitmap creation,
    /// print error message on save-to-disk failure
    let jpgHeatMap fullname colorTable size (matrix: (int list) list) =
        use bitmap = new Bitmap( ((matrix.Length+1)*size), ((matrix.Head.Length+1)*size) )
        let drawElement x i j = 
            let color = colorTable x
            for k in [i*size .. ((i+1)*size)] do
                for l in [j*size .. ((j+1)*size)] do
                    bitmap.SetPixel( k, l, color)
        matrix 
        |> List.iteri (fun i row -> row |> List.iteri (fun j x -> drawElement x i j))
        try bitmap.Save(fullname) with | _ -> printfn "saving %s failed." fullname
        

///
///     Simulation of a chromosome evolution across 75 generations
///

/// common settings for the simulation

open EvolutionaryComputation

let lociFunction() = random.Next(10)

let fitnessF (items: int list) = items |> List.sum 

let populationSize = 50
let chromosomeSize = 10
let myPopulation = Population<int,int> lociFunction fitnessF id populationSize chromosomeSize

let elitism = true
let selectionF (x: ChromosomeType<int,int> list) = RankSelection<int,int> x
let shuffleCrossover x y = ShuffleCrossover<int,int> x y
let crossoverRate = 0.9
let mutationRate = 0.1

/// convenience function for the evolution
let evolve (x: ChromosomeType<int,int> list) = 
    Evolve<int,int>(x, selectionF, shuffleCrossover, crossoverRate, mutationRate, elitism)

/// simulate the evolution across x generations
let generations x = 
    let rec doEvolve pop acc x = 
        if x > 0 then
            let tail = (evolve pop)
            doEvolve tail (acc @ [tail]) (x-1)
        else acc
    doEvolve myPopulation [] x
    
/// simulation result
let history = generations 75
/// compare ancestor vs. descendant
printfn "max. fitness of the original population: %A" (maxFitness<int,int> history.Head)
let lastPopulation = history |> List.rev |> List.head
printfn "max. fitness of the resulting population: %A" (maxFitness<int,int> lastPopulation)

/// settings for presenting the animation in a window form

let dimX = populationSize
let dimY = chromosomeSize
/// match color table with chromosome size 
let colorTable i = Drawing.defaultColorTable i 
/// set heat map item size
let size = 10

open System.Drawing
open System.Windows.Forms

/// show the animation  in a window form

let form = new Form(Visible=true, ClientSize=Size(dimX*size, dimY*size))
form.Show()

let mutable repeat = true
let yes = ["y"; "Y"; "j"; "J"] |> Set.ofList
while repeat do
    for h in history do
        [for x in h do yield x.Genos; yield x.Pheno]
        |> Drawing.heatMap form colorTable size
        form.ResetForeColor()
    System.Console.WriteLine "repeat? "
    repeat <- yes.Contains(System.Console.ReadLine())
form.Close()

/// settings for presenting the animation in avi format 

let jpgDir = @"D:\projects\GeneticAlgorithm\jpg\"

/// use a library for writing avi files: 
/// http://www.codeproject.com/Articles/7388/A-Simple-C-Wrapper-for-the-AviFile-Library
#r @"D:\projects\GeneticAlgorithm\jpg\AviFile.dll"

open AviFile
open System.IO

/// helper functions

/// calculate the decimal points of an integer
/// add 1 decimal point for a negative integer
let decimalPoints i : int =  
    let rec f i_ d =
        if i_ < 10 then d + 1
                   else f (i_/10) (d+1)
    if i < 0 then (f (-1*i) 1) else (f i 0)

/// repeat a letter as often as requested
/// return empty string for requests less than 1
let repeatChar letter times =
    let rec f i c =
        if i > 0 then c + letter.ToString()
                 else c
    if times < 1 then "" else f (times-1) (letter.ToString())

/// return a numbered file name pattern with sufficient leading zeros
let jpgNamePattern tab i = 
    let tabZero = ((tab |> decimalPoints) - (i |> decimalPoints)) |> repeatChar '0'
    jpgDir + "HeatMap-" + tabZero + i.ToString() + ".jpg"

/// match files by extention
let matchExt (f: FileInfo) (ext: string) =
    let (|FileInfoNameSections|) (f:FileInfo) =
             (f.Name,f.Extension,f.FullName)
    (if f.Extension.ToLower() = ext.ToLower() then (Some f.FullName) else None) 

/// Step 1: write the heat maps to disk

/// write the previously calculated history of evolutions  to disk
let mutable i = 0
for h in history do
    i <- i+1 
    [for x in h do yield x.Genos; yield x.Pheno]
    |> Drawing.jpgHeatMap (jpgNamePattern history.Length i) colorTable size

/// Step 2: prepare the working queue

/// retrieve jpg files from jpg directory, 
/// sorted ascending by filename
let jpgs = 
    [ for file in System.IO.Directory.GetFiles jpgDir do 
        let fileinfo = try Some (new FileInfo (file)) with | _ -> None
        match fileinfo with
        | None -> ignore fileinfo
        | Some fileinfo -> 
            let fullname = (matchExt fileinfo ".jpg")
            match fullname with
            | None -> ignore fullname
            | Some x -> yield x ]
    |> List.sort

/// Step 3: pipe the jpg file into an avi file 

/// convert jpg files into avi file
if jpgs.Length > 0 then
    try 
        /// load the first image
        use initBitmap = (Bitmap.FromFile(jpgs.Head) :?> Bitmap)
        /// create a new AVI file
        let aviManager = new AviManager (jpgDir + "GeneticAlgorithm.avi", false)
        /// create a new video stream and one frame to the new file
        let aviStream = aviManager.AddVideoStream (true, 6., initBitmap)
        /// add bitmaps to video stream
        for j in jpgs.Tail do
            use bitmap = (Bitmap.FromFile( j ) :?> Bitmap)
            aviStream.AddFrame(bitmap)
        /// clean up
        aviManager.Close()
    with | ex -> ignore ex
