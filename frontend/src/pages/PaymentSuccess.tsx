import { Link } from 'react-router-dom';
import { useEffect } from 'react';
import { useSubscription } from '../contexts/SubscriptionContext';

export default function PaymentSuccess() {
  const { refresh } = useSubscription();

  // Refresh subscription status after successful payment
  useEffect(() => {
    const timer = setTimeout(() => refresh(), 2000);
    return () => clearTimeout(timer);
  }, [refresh]);

  return (
    <div className="min-h-screen bg-gray-900 flex items-center justify-center px-4">
      <div className="max-w-md w-full text-center">
        {/* Success Icon */}
        <div className="mx-auto w-20 h-20 bg-green-500/20 rounded-full flex items-center justify-center mb-6">
          <svg
            className="w-10 h-10 text-green-400"
            fill="none"
            stroke="currentColor"
            viewBox="0 0 24 24"
          >
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              strokeWidth={2}
              d="M5 13l4 4L19 7"
            />
          </svg>
        </div>

        <h1 className="text-3xl font-bold text-white mb-3">
          Thanks for subscribing! 🎉
        </h1>
        <p className="text-gray-400 mb-8">
          Your subscription is now active. You have access to all premium features.
        </p>

        <div className="space-y-3">
          <Link
            to="/"
            className="block w-full py-3 px-6 rounded-lg bg-blue-600 text-white font-medium hover:bg-blue-700 transition-colors"
          >
            Go to Dashboard
          </Link>
          <Link
            to="/account"
            className="block w-full py-3 px-6 rounded-lg bg-gray-800 text-gray-300 font-medium hover:bg-gray-700 transition-colors border border-gray-700"
          >
            View Subscription Details
          </Link>
        </div>
      </div>
    </div>
  );
}
