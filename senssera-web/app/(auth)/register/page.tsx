import Link from "next/link";
import { AuthCard } from "@/components/auth/auth-card";
import { RegisterForm } from "@/components/auth/register-form";

export default function RegisterPage() {
  return (
    <AuthCard
      eyebrow="New organization"
      title="Start a notebook"
      description="Create an account and your first greenhouse in a couple of minutes."
      footer={
        <>
          Already have an account?{" "}
          <Link href="/login" className="font-serif italic text-foreground underline underline-offset-4">
            Sign in
          </Link>
        </>
      }
    >
      <RegisterForm />
    </AuthCard>
  );
}
