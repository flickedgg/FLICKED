import ancient from "../assets/Ancient.jpg";
import anubis from "../assets/Anubis.jpg";
import dust from "../assets/Dust.jpg";
import inferno from "../assets/Inferno.jpeg";
import mirage from "../assets/Mirage.jpg";
import nuke from "../assets/Nuke.jpeg";
import train from "../assets/Train.png";

/* Map art, keyed by the name screens show.

   Imported rather than built from a string: Vite then hashes each file, bundles
   it, and a typo becomes a build error instead of a missing image nobody
   notices until a vote is on screen. */
export const MAP_IMAGE: Record<string, string> = {
  Mirage: mirage,
  Inferno: inferno,
  Nuke: nuke,
  Ancient: ancient,
  Anubis: anubis,
  "Dust II": dust,
  Train: train,
};
