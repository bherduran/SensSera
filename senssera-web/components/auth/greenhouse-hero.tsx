"use client";

import { useEffect, useRef, useState } from "react";
import Image from "next/image";
import { useTheme } from "next-themes";
import { useReducedMotion } from "motion/react";
import type { GreenhouseScene, HeroTheme } from "@/lib/hero/greenhouse-scene";
import { cn } from "@/lib/utils";

/**
 * Live clay greenhouse. The static render shows while three.js loads (and stays for reduced
 * motion or when WebGL is unavailable); then the real-time scene fades in over it.
 */
export function GreenhouseHero({ className }: { className?: string }) {
  const wrapRef = useRef<HTMLDivElement>(null);
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const sceneRef = useRef<GreenhouseScene | null>(null);
  const [ready, setReady] = useState(false);
  const { resolvedTheme } = useTheme();
  const reduced = useReducedMotion();
  const theme: HeroTheme = resolvedTheme === "dark" ? "dark" : "light";
  const themeRef = useRef(theme);

  useEffect(() => {
    themeRef.current = theme;
    sceneRef.current?.setTheme(theme);
  }, [theme]);

  useEffect(() => {
    if (reduced) return;
    const wrap = wrapRef.current, canvas = canvasRef.current;
    if (!wrap || !canvas) return;

    let raf = 0;
    let disposed = false;
    let observer: ResizeObserver | undefined;
    const start = performance.now();

    const onPointer = (e: PointerEvent) => {
      const r = wrap.getBoundingClientRect();
      sceneRef.current?.setPointer(((e.clientX - r.left) / r.width) * 2 - 1, ((e.clientY - r.top) / r.height) * 2 - 1);
    };
    const loop = () => {
      sceneRef.current?.frame((performance.now() - start) / 1000);
      raf = requestAnimationFrame(loop);
    };
    // Hover makes the model lean in; press-and-drag spins it (inertia is handled in the scene).
    let dragX: number | null = null;
    let dragAt = 0;
    const onEnter = () => sceneRef.current?.setHover(true);
    const onLeave = () => sceneRef.current?.setHover(false);
    const onDown = (e: PointerEvent) => {
      dragX = e.clientX;
      dragAt = performance.now();
      wrap.setPointerCapture(e.pointerId);
      wrap.dataset.dragging = "true";
      sceneRef.current?.dragStart();
    };
    const onMove = (e: PointerEvent) => {
      if (dragX === null) return;
      const now = performance.now();
      sceneRef.current?.dragBy((e.clientX - dragX) / wrap.clientWidth, (now - dragAt) / 1000);
      dragX = e.clientX;
      dragAt = now;
    };
    const onUp = (e: PointerEvent) => {
      if (dragX === null) return;
      dragX = null;
      if (wrap.hasPointerCapture(e.pointerId)) wrap.releasePointerCapture(e.pointerId);
      delete wrap.dataset.dragging;
      sceneRef.current?.dragEnd();
    };
    const onVisibility = () => {
      cancelAnimationFrame(raf);
      if (!document.hidden && sceneRef.current) raf = requestAnimationFrame(loop);
    };

    import("@/lib/hero/greenhouse-scene")
      .then(({ GreenhouseScene }) => {
        if (disposed) return;
        const scene = new GreenhouseScene(canvas, themeRef.current);
        sceneRef.current = scene;
        const size = () => scene.resize(wrap.clientWidth, wrap.clientHeight);
        size();
        observer = new ResizeObserver(size);
        observer.observe(wrap);
        scene.frame(0);
        setReady(true);
        raf = requestAnimationFrame(loop);
      })
      .catch(() => {
        // No WebGL (or the chunk failed): the static render simply stays.
      });

    window.addEventListener("pointermove", onPointer, { passive: true });
    wrap.addEventListener("pointerenter", onEnter);
    wrap.addEventListener("pointerleave", onLeave);
    wrap.addEventListener("pointerdown", onDown);
    wrap.addEventListener("pointermove", onMove);
    wrap.addEventListener("pointerup", onUp);
    wrap.addEventListener("pointercancel", onUp);
    document.addEventListener("visibilitychange", onVisibility);
    return () => {
      disposed = true;
      cancelAnimationFrame(raf);
      observer?.disconnect();
      window.removeEventListener("pointermove", onPointer);
      wrap.removeEventListener("pointerenter", onEnter);
      wrap.removeEventListener("pointerleave", onLeave);
      wrap.removeEventListener("pointerdown", onDown);
      wrap.removeEventListener("pointermove", onMove);
      wrap.removeEventListener("pointerup", onUp);
      wrap.removeEventListener("pointercancel", onUp);
      document.removeEventListener("visibilitychange", onVisibility);
      sceneRef.current?.dispose();
      sceneRef.current = null;
    };
  }, [reduced]);

  const fallback = "object-contain transition-opacity duration-700";
  return (
    // Soft vignette mask: the floor shadow fades into the page instead of stopping at the canvas edge.
    <div
      ref={wrapRef}
      className={cn(
        "relative cursor-grab touch-pan-y select-none data-[dragging]:cursor-grabbing [mask-image:linear-gradient(to_right,black_68%,transparent_98%),linear-gradient(to_bottom,black_78%,transparent_99%)] [mask-composite:intersect]",
        className,
      )}
    >
      <Image
        src="/hero/light.webp"
        alt="Clay model of a small glass greenhouse with planter beds, a terracotta pot and a sensor"
        fill
        priority
        sizes="(min-width: 1024px) 55vw, 100vw"
        className={cn(fallback, "dark:hidden", ready && "opacity-0")}
      />
      <Image
        src="/hero/dark.webp"
        alt=""
        fill
        priority
        sizes="(min-width: 1024px) 55vw, 100vw"
        className={cn(fallback, "hidden dark:block", ready && "opacity-0")}
      />
      <canvas
        ref={canvasRef}
        aria-hidden
        className={cn("absolute inset-0 size-full transition-opacity duration-700", ready ? "opacity-100" : "opacity-0")}
      />
    </div>
  );
}
