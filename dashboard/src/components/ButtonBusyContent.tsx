interface ButtonBusyContentProps {
  Busy: boolean;
  Label: string;
  BusyLabel?: string;
}

export default function ButtonBusyContent({ Busy, Label, BusyLabel }: ButtonBusyContentProps) {
  if (!Busy) {
    return <>{Label}</>;
  }

  return (
    <span className="button-busy-content">
      <span className="button-spinner" aria-hidden="true" />
      <span className="sr-only">{BusyLabel || Label}</span>
    </span>
  );
}
