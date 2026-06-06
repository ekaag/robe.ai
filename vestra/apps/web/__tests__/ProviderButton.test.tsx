import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { ProviderButton } from "../components/ProviderButton";

describe("ProviderButton", () => {
  it("renders the label", () => {
    render(<ProviderButton provider="google" label="Continue with Google" onClick={vi.fn()} />);
    expect(screen.getByRole("button", { name: /continue with google/i })).toBeInTheDocument();
  });

  it("calls onClick when clicked", () => {
    const handler = vi.fn();
    render(<ProviderButton provider="apple" label="Continue with Apple" onClick={handler} />);
    fireEvent.click(screen.getByRole("button"));
    expect(handler).toHaveBeenCalledOnce();
  });

  it("does not call onClick when not clicked", () => {
    const handler = vi.fn();
    render(<ProviderButton provider="facebook" label="Continue with Facebook" onClick={handler} />);
    expect(handler).not.toHaveBeenCalled();
  });
});
