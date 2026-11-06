import type { Metadata } from "next";
import { BoardSetup } from "@/components/setup/board-setup";

// Public on purpose: the friend hosting the rig installs and configures the board here without
// an account. Nothing on the page calls the API (only the root layout's session check does), and
// the token only goes from the browser to the board.
export const metadata: Metadata = {
  title: "Kart kurulumu — SensSera",
  description: "Saksı Pilotu kartına firmware yükle, Wi-Fi ve token ayarla, nem sensörünü kalibre et.",
};

export default function SetupPage() {
  return (
    <main className="mx-auto w-full max-w-3xl space-y-8 px-4 py-10 sm:px-6">
      <header className="pb-3 ink-rule">
        <p className="label-caps">Saksı Pilotu · kurulum</p>
        <h1 className="mt-1.5 font-serif text-[34px] leading-none">Kartı kur ve güncelle</h1>
        <p className="mt-3 text-sm text-muted-foreground">
          Masaüstünde Chrome veya Edge kullanın. Kartı <strong>USB</strong> yazan portundan bilgisayara takın
          (COM yazan değil) ve veri taşıyan bir kablo kullanın.
        </p>
      </header>
      <BoardSetup />
    </main>
  );
}
