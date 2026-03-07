let connection = null;
let currentUserId = null;
let currentServerUrl = "";
let countdownIntervals = {}; // Stores setInterval IDs to prevent memory leaks

//  Establish the SignalR Connection
async function connectToServer() {
    const serverSelect = document.getElementById("serverSelect");
    currentServerUrl = serverSelect.value;

    document.getElementById("connectBtn").disabled = true;
    document.getElementById("connectionStatus").innerText = "Connecting...";

    // Build the connection pointing to the mapped Hub route
    connection = new signalR.HubConnectionBuilder()
        .withUrl(currentServerUrl + "/auctionHub")
        .withAutomaticReconnect()
        .configureLogging(signalR.LogLevel.Information)
        .build();

    registerClientMethods();

    try {
        await connection.start();
        document.getElementById("connectionStatus").className = "status-connected";
        document.getElementById("connectionStatus").innerText = "Connected";
        document.getElementById("connectBtn").style.display = "none";
        document.getElementById("serverSelect").disabled = true;
        document.getElementById("restartBtn").disabled = false;
    } catch (err) {
        console.error(err);
        document.getElementById("connectionStatus").innerText = "Connection Failed";
        document.getElementById("connectBtn").disabled = false;
        showToast("Failed to connect to server.");
    }
}

//  Register Client-Side Methods (Matches IAuctionNotificationClient)
function registerClientMethods() {
    
    // Task Initialize(List<AuctionDTO> auctions, string UserID);
    connection.on("Initialize", (auctions, userId) => {
        currentUserId = userId;
        document.getElementById("userIdDisplay").innerText = userId;
        renderAllAuctions(auctions);
    });

    // Task RestartAllAuctions(List<AuctionDTO> auctions);
    connection.on("RestartAllAuctions", (auctions) => {
        showToast("System Restarted: All auctions have been reset.", true);
        renderAllAuctions(auctions);
    });

    // Task ShowUserMessage(string Message);
    connection.on("ShowUserMessage", (message) => {
        showToast(message);
    });

    // Task UpdateAuction(AuctionDTO auction);
    connection.on("UpdateAuction", (auction) => {
        updateSingleAuctionCard(auction);
    });
}

//  UI Rendering Logic
function renderAllAuctions(auctions) {
    const container = document.getElementById("auctionsContainer");
    container.innerHTML = ""; // Clear existing

    // Clear old timers
    Object.values(countdownIntervals).forEach(clearInterval);
    countdownIntervals = {};

    auctions.forEach(auction => {
        container.appendChild(createAuctionElement(auction));
        startCountdownTimer(auction.id, auction.endingTime);
    });
}

function createAuctionElement(auction) {
    const div = document.createElement("div");
    div.className = "auction-card";
    div.id = `auction-card-${auction.id}`;

    // Get highest bidder (last item in WinnersIDs array that isn't null)
    const validWinners = auction.winnersIDs.filter(id => id !== null);
    const topBidder = validWinners.length > 0 ? validWinners[validWinners.length - 1] : "No bids yet";

    div.innerHTML = `
        <h3 class="auction-title">Auction #${auction.id}</h3>
        <div id="timer-${auction.id}" class="timer">Calculating time...</div>
        <div id="price-${auction.id}" class="price-display">$${auction.highestBidValue.toFixed(2)}</div>
        
        <div class="bid-controls">
            <input type="number" id="bid-input-${auction.id}" placeholder="Enter bid amount" min="${auction.highestBidValue + 1}">
            <button onclick="placeBid(${auction.id})">Place Bid</button>
        </div>
        
        <div class="winner-display">
            <strong>Current Winner:</strong> <span id="winner-${auction.id}">${topBidder}</span>
        </div>
    `;
    return div;
}

function updateSingleAuctionCard(auction) {
    const priceEl = document.getElementById(`price-${auction.id}`);
    const inputEl = document.getElementById(`bid-input-${auction.id}`);
    const winnerEl = document.getElementById(`winner-${auction.id}`);

    if (priceEl && inputEl && winnerEl) {
        priceEl.innerText = `$${auction.highestBidValue.toFixed(2)}`;
        inputEl.min = auction.highestBidValue + 1;
        inputEl.value = ""; // Clear input after successful bid
        
        const validWinners = auction.winnersIDs.filter(id => id !== null);
        winnerEl.innerText = validWinners.length > 0 ? validWinners[validWinners.length - 1] : "No bids yet";
    }
}

// Time Calculation
function startCountdownTimer(id, endingTimeString) {
    const timerEl = document.getElementById(`timer-${id}`);
    const endingDate = new Date(endingTimeString).getTime();

    countdownIntervals[id] = setInterval(() => {
        const now = new Date().getTime();
        const distance = endingDate - now;

        if (distance < 0) {
            clearInterval(countdownIntervals[id]);
            timerEl.innerText = "AUCTION ENDED";
            timerEl.classList.add("ended");
            document.getElementById(`bid-input-${id}`).disabled = true;
            return;
        }

        const minutes = Math.floor((distance % (1000 * 60 * 60)) / (1000 * 60));
        const seconds = Math.floor((distance % (1000 * 60)) / 1000);
        
        timerEl.classList.remove("ended");
        document.getElementById(`bid-input-${id}`).disabled = false;
        timerEl.innerText = `Time Left: ${minutes.toString().padStart(2, '0')}:${seconds.toString().padStart(2, '0')}`;
    }, 1000);
}

// User Actions Triggered by Buttons
async function placeBid(auctionId) {
    const inputEl = document.getElementById(`bid-input-${auctionId}`);
    const bidValue = parseFloat(inputEl.value);

    if (isNaN(bidValue)) {
        showToast("Please enter a valid number.");
        return;
    }

    try {
        // Calls the Hub method: Task Bid(string auctionID, double value)
        await connection.invoke("Bid", auctionId.toString(), bidValue);
    } catch (err) {
        console.error("Bid failed:", err);
        showToast("Error communicating with server.");
    }
}

async function restartAuctions() {
    try {
        // Calls the Minimal API endpoint
        const response = await fetch(`${currentServerUrl}/restart`, {
            method: 'PUT'
        });
        
        if (!response.ok) {
            showToast("Failed to trigger restart on the server.");
        }
    } catch (err) {
        console.error(err);
        showToast("Network error while trying to restart.");
    }
}

// Toast Utility
function showToast(message, isSuccess = false) {
    const container = document.getElementById("toastContainer");
    const toast = document.createElement("div");
    toast.className = "toast";
    if (isSuccess) toast.style.backgroundColor = "#28a745"; // Green for success
    toast.innerText = message;
    
    container.appendChild(toast);
    
    // Auto-remove toast after 5 seconds
    setTimeout(() => {
        toast.remove();
    }, 5000);
}