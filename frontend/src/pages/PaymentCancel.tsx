import { Link } from 'react-router-dom';

export default function PaymentCancel() {
  return (
    <div className="min-h-screen bg-gray-900 flex items-center justify-center px-4">
      <div className="max-w-md w-full text-center">
        {/* Info Icon */}
        <div className="mx-auto w-20 h-20 bg-gray-700 rounded-full flex items-center justify-center mb-6">
          <svg
            className="w-10 h-10 text-gray-400"
            fill="none"
            stroke="currentColor"
            viewBox="0 0 24 24"
          >
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              strokeWidth={2}
              d="M6 18L18 6M6 6l12 12"
            />
          </svg>
        </div>

        <h1 className="text-3xl font-bold text-white mb-3">
          No worries!
        </h1>
        <p className="text-gray-400 mb-8">
          You can upgrade to a premium plan anytime. Your current plan continues as usual.
        </p>

        <div className="space-y-3">
          <Link
            to="/pricing"
            className="block w-full py-3 px-6 rounded-lg bg-blue-600 text-white font-medium hover:bg-blue-700 transition-colors"
          >
            View Plans Again
          </Link>
          <Link
            to="/"
            className="block w-full py-3 px-6 rounded-lg bg-gray-800 text-gray-300 font-medium hover:bg-gray-700 transition-colors border border-gray-700"
          >
            Back to Dashboard
          </Link>
        </div>
      </div>
    </div>
  );
}
