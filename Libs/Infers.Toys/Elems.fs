﻿// Copyright (C) by Vesa Karvonen

module Infers.Toys.Elems

open Infers
open Infers.Rep

type Elems<'h, 'w> = 'w -> array<'h>

type ElemsP<'e, 'r, 'o, 'h, 'w> =
  | Miss
  | Hit of num: int * ext: ('w -> int -> array<'h> -> unit)
type ElemsS<'p, 'o, 'h, 'w> = SE of list<Elems<'h, 'w>>

let missE _ = [||]

type [<InferenceRules>] Elems () =
  inherit Rep ()
  member g.Elem0 (_: Elem<'e, 'r, 'o, 'w>) : ElemsP<'e, 'r, 'o, 'h, 'w> =
    Miss
  member g.Elem1 (hE: Elem<'h, 'r, 'o, 'w>) : ElemsP<'h, 'r, 'o, 'h, 'w> =
    Hit (1, fun w i hs -> hs.[i] <- hE.Get w)
  member g.Pair (eF: ElemsP<     'e     , Pair<'e, 'r>, 'o, 'p, 'w>,
                 rF: ElemsP<         'r ,          'r , 'o, 'p, 'w>)
                   : ElemsP<Pair<'e, 'r>, Pair<'e, 'r>, 'o, 'p, 'w> =
    match (eF, rF) with
     | (Miss, Miss) -> Miss
     | (Hit (n, e), Miss) | (Miss, Hit (n, e)) -> Hit (n, e)
     | (Hit (numE, extE), Hit (numR, extR)) ->
       Hit (numE + numR, fun w i hs -> extE w i hs; extR w (i + numE) hs)
  member g.Product (_: AsPairs<'p, 'o, 'w>, pF: ElemsP<'p, 'p, 'o, 'h, 'w>) =
    match pF with
     | Miss -> missE
     | Hit (n, ext) -> fun w -> let hs = Array.zeroCreate n in ext w 0 hs; hs
  member g.Case (_: Case<Empty, 'o, 'w>) : ElemsS<Empty, 'o, 'h, 'w> =
    SE [missE]
  member g.Case (m: Case<'p, 'o, 'w>, pF: ElemsP<'p, 'p, 'o, 'h, 'w>) =
    SE [g.Product (m, pF)] : ElemsS<'p, 'o, 'h, 'w>
  member g.Choice (SE pF: ElemsS<       'p     , Choice<'p, 'o>, 'h, 'w>,
                   SE oF: ElemsS<           'o ,            'o , 'h, 'w>) =
    SE (pF @ oF)        : ElemsS<Choice<'p, 'o>, Choice<'p, 'o>, 'h, 'w>
  member g.Sum (m: AsChoices<'s, 'w>, SE sF: ElemsS<'s, 's, 'h, 'w>) =
    let sF = Array.ofList sF
    fun w -> sF.[m.Tag w] w

let elems<'h, 'w> w = Engine.generate<Elems, Elems<'h, 'w>> w

////////////////////////////////////////////////////////////////////////////////

let children w = elems<'w, 'w> w

let rec elemsDn<'h, 'w> (w: 'w) : seq<'h> = Seq.delay <| fun () ->
  Seq.append (elems w) (Seq.collect elemsDn (children w))

let universe w = Seq.append (Seq.singleton w) (elemsDn w)

////////////////////////////////////////////////////////////////////////////////

type Subst<'h, 'w> = ('h -> 'h) -> 'w -> 'w

type [<AbstractClass;AllowNullLiteral>] SubstP<'e, 'r, 'o, 'h, 'w> () =
  abstract Subst: ('h -> 'h) * byref<'e> -> unit

type SubstS<'p, 'o, 'h, 'w> = SS of list<Subst<'h, 'w>>

type [<InferenceRules>] Subst () =
  inherit Rep ()
  member g.Elem0 (_: Elem<'e, 'r, 'o, 'w>) : SubstP<'e, 'r, 'o, 'h, 'w> =
    null
  member g.Elem1 (_: Elem<'h, 'r, 'o, 'w>) =
    {new SubstP<'h, 'r, 'o, 'h, 'w> () with
      member t.Subst (h2h, h) = h <- h2h h}
  member g.Pair (eS: SubstP<     'e     , Pair<'e, 'r>, 'o, 'h, 'w>,
                 rS: SubstP<         'r ,          'r , 'o, 'h, 'w>)
                   : SubstP<Pair<'e, 'r>, Pair<'e, 'r>, 'o, 'h, 'w> =
    match (eS, rS) with
     | (null, null) -> null
     | (eS, null) ->
       {new SubstP<Pair<'e, 'r>, Pair<'e, 'r>, 'o, 'h, 'w> () with
          member t.Subst (h2h, er) = eS.Subst (h2h, &er.Elem)}
     | (null, rS) ->
       {new SubstP<Pair<'e, 'r>, Pair<'e, 'r>, 'o, 'h, 'w> () with
          member t.Subst (h2h, er) = rS.Subst (h2h, &er.Rest)}
     | (eS, rS) ->
       {new SubstP<Pair<'e, 'r>, Pair<'e, 'r>, 'o, 'h, 'w> () with
          member t.Subst (h2h, er) =
           eS.Subst (h2h, &er.Elem)
           rS.Subst (h2h, &er.Rest)}
  member g.Product (m: AsPairs<'p, 'o, 'w>, pS: SubstP<'p, 'p, 'o, 'h, 'w>) =
    match pS with
     | null -> fun _ w -> w
     | pS -> fun h2h w ->
       let mutable p = m.ToPairs w
       pS.Subst (h2h, &p)
       m.Create (&p)
  member g.Case (_: Case<Empty, 'o, 'w>) : SubstS<Empty, 'o, 'h, 'w> =
    SS [fun _ w -> w]
  member g.Case (m: Case<'p, 'o, 'w>, pS: SubstP<'p, 'p, 'o, 'h, 'w>) =
    SS [g.Product (m, pS)] : SubstS<'p, 'o, 'h, 'w>
  member g.Choice (SS pS: SubstS<       'p     , Choice<'p, 'o>, 'h, 'w>,
                   SS oS: SubstS<           'o ,            'o , 'h, 'w>) =
    SS (pS @ oS)        : SubstS<Choice<'p, 'o>, Choice<'p, 'o>, 'h, 'w>
  member g.Sum (m: AsChoices<'s, 'w>, SS s: SubstS<'s, 's, 'p, 'w>) =
    let s = Array.ofList s
    fun hs w -> s.[m.Tag w] hs w

let subst<'h, 'w> h2h w = Engine.generate<Subst, Subst<'h, 'w>> h2h w

////////////////////////////////////////////////////////////////////////////////

let rec substUp<'h, 'w> (h2h: 'h -> 'h) (w: 'w) =
  w |> subst (substUp h2h) |> subst h2h

let transform w2w w = w |> substUp w2w |> w2w

let rec rewrite w2wO w =
  transform <| fun w -> match w2wO w with
                         | None -> w
                         | Some w -> rewrite w2wO w
            <| w

let rec para op w =
  children w
  |> Array.map (para op)
  |> op w