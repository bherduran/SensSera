import Link from "next/link";
import { AuthCard } from "@/components/auth/auth-card";
import { LoginForm } from "@/components/auth/login-form";

export default function LoginPage() {
  return (
    <AuthCard
      eyebrow="Sign in"
      title="Open your notebook"
      description="Pick up where your greenhouses left off."
      footer={
        <>
          Don&apos;t have an account?{" "}
          <Link href="/register" className="font-serif italic text-foreground underline underline-offset-4">
            Create one
          </Link>
        </>
      }
    >
      <LoginForm />
    </AuthCard>
  );
}
