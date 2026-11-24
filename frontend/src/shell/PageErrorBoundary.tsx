import { Component, type ErrorInfo, type ReactNode } from 'react';

interface PageErrorBoundaryProps {
  children: ReactNode;
}

interface PageErrorBoundaryState {
  failed: boolean;
}

/** AC-229: a page that fails while it is drawn shows a message instead of a blank page; the shell around it stays. */
export class PageErrorBoundary extends Component<PageErrorBoundaryProps, PageErrorBoundaryState> {
  override state: PageErrorBoundaryState = { failed: false };

  static getDerivedStateFromError(): PageErrorBoundaryState {
    return { failed: true };
  }

  // The cause stays in the console for diagnosis.
  override componentDidCatch(error: Error, info: ErrorInfo): void {
    console.error(error, info.componentStack);
  }

  override render(): ReactNode {
    if (this.state.failed) {
      return (
        <p role="alert" className="problem">
          Something went wrong in the app. Reload the page and try again.
        </p>
      );
    }
    return this.props.children;
  }
}
