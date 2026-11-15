# Kazo Deck

Un éditeur de présentations pour Kazo OS. Kazo Deck est basé sur
[DeckCraft](https://github.com/storytold/deckcraft) de l'équipe ArtCraft, un éditeur de
présentations libre écrit en Rust, qui tourne ici dans une fenêtre de Kazo OS.

## Faire des présentations

- Masques et dispositions de diapositives, thèmes avec jeux de couleurs et de polices, sections,
  commentaires, notes, en-têtes et pieds de page, taille de diapositive au choix.
- Plus de 150 formes avec poignées d'ajustement, connecteurs, dessin libre, fusion de formes,
  dégradés, ombres, lueurs et reflets.
- Texte mis en forme dans la diapositive : polices, paragraphes, puces et numéros, colonnes,
  ajustement automatique, liens.
- Images, tableaux, graphiques, icônes, audio et vidéo.
- Transitions (dont Morph), animations d'entrée, d'emphase, de sortie et trajectoires.
- Modèles de départ pour commencer une présentation en quelques secondes.

## Présenter

- F5 lance le diaporama depuis le début, Maj+F5 depuis la diapositive en cours, et le bouton
  « Present » aussi : la présentation passe en plein écran dans le navigateur. Échap, ou la fin
  de la dernière diapositive, ramène à la fenêtre.
- Si le navigateur refuse le plein écran, le diaporama remplit la fenêtre.

## Vos fichiers Kazo OS

- Double-cliquez sur un `.pptx`, un `.ppsx`, un `.potx` ou un `.deckcraft` dans l'Explorateur de
  fichiers : il s'ouvre dans Kazo Deck. Les fichiers `.txt`, `.md` et `.rtf` le proposent dans
  « Ouvrir avec » (un plan devient des diapositives).
- Une présentation par fenêtre : ouvrir un autre fichier, ou Ctrl+N, ouvre une nouvelle fenêtre.
- Enregistrer réécrit le fichier ouvert, au même format. Enregistrer sous, et le premier
  enregistrement d'une nouvelle présentation, passent par le dialogue d'enregistrement de Kazo OS.
- Exporter (PowerPoint, PDF, images, GIF animé, plan) passe par le dialogue « Enregistrer une
  copie » de Kazo OS : la présentation reste liée à son fichier. Les images de toutes les
  diapositives se rangent dans le dossier choisi pour la première. Rien ne part dans les
  téléchargements du navigateur.
- Fermer la fenêtre demande avant de perdre des modifications.
- Kazo Deck suit le thème clair ou sombre de Kazo OS.

## Limites connues

- **DeckCraft est en alpha.** La plupart des présentations s'ouvrent et s'enregistrent bien, mais
  gardez une copie des plus importantes.
- L'interface de DeckCraft est en anglais.
- Kazo OS ouvre les fichiers jusqu'à 128 Mio.
- DeckCraft dans le navigateur n'intègre que deux polices : Inter et BIZ UDPGothic (japonais). Une
  présentation écrite avec une autre police (Calibri, Arial, Lato…) s'affiche en Inter, et sa mise
  en page peut bouger un peu ; le fichier garde ses polices. Le texte chinois, coréen, arabe, hébreu
  ou thaï peut s'afficher en carrés.
- Le mode présentateur (notes et diapositive suivante sur un autre écran) est coupé pour
  l'instant.
- La fenêtre reste sur l'écran où elle s'est ouverte : la passer sur un autre écran rechargerait
  DeckCraft et perdrait la présentation ouverte.
- DeckCraft dessine avec WebGPU ou WebGL 2 : un navigateur ou un ordinateur sans l'un ni l'autre
  montre un écran d'erreur au lieu des diapositives.
- Le téléchargement fait environ 15 Mo (environ 40 Mo une fois installé), et chaque fenêtre charge
  sa propre copie de DeckCraft.

## Licence

Kazo Deck est un logiciel libre sous licence MIT. DeckCraft est sous licence MIT ou Apache-2.0 ;
ses avis, les licences du matériel qu'il contient, et celles des polices (SIL Open Font License
1.1) sont livrés avec l'app (voir `THIRD-PARTY-NOTICES-fr.md` dans le dépôt). Le nom et les logos
ArtCraft sont des marques de l'équipe ArtCraft et ne sont pas utilisés par Kazo Deck.
