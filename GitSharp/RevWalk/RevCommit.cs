/*
 * Copyright (C) 2008, Shawn O. Pearce <spearce@spearce.org>
 * Copyright (C) 2009, Henon <meinrad.recheis@gmail.com>
 *
 * All rights reserved.
 *
 * Redistribution and use in source and binary forms, with or
 * without modification, are permitted provided that the following
 * conditions are met:
 *
 * - Redistributions of source code must retain the above copyright
 *   notice, this list of conditions and the following disclaimer.
 *
 * - Redistributions in binary form must reproduce the above
 *   copyright notice, this list of conditions and the following
 *   disclaimer in the documentation and/or other materials provided
 *   with the distribution.
 *
 * - Neither the name of the Git Development Community nor the
 *   names of its contributors may be used to endorse or promote
 *   products derived from this software without specific prior
 *   written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND
 * CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES,
 * INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES
 * OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR
 * CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
 * SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT
 * NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
 * CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT,
 * STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
 * ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF
 * ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Text;
using GitSharp.Exceptions;
using GitSharp.Util;

namespace GitSharp.RevWalk
{
    /// <summary>
	/// A commit reference to a commit in the DAG.
    /// </summary>
    public class RevCommit : RevObject
    {
        public static readonly RevCommit[] NO_PARENTS = { };

        private RevTree _tree;
    	private byte[] _buffer;

		/// <summary>
		/// Create a new commit reference.
		/// </summary>
		/// <param name="id">object name for the commit.</param>
        public RevCommit(AnyObjectId id)
            : base(id)
        {
        }

		/// <summary>
		/// Gets the time from the "committer " line of the buffer.
		/// </summary>
    	public int CommitTime { get; set; }

    	public int InDegree { get; set; }

		/// <summary>
		/// Obtain an array of all parents (<b>NOTE - THIS IS NOT A COPY</b>).
		/// <para />
		/// This method is exposed only to provide very fast, efficient access to
		/// this commit's parent list. Applications relying on this list should be
		/// very careful to ensure they do not modify its contents during their use
		/// of it.
		/// </summary>
    	public RevCommit[] Parents { get; set; }

		/// <summary>
		/// Get a reference to this commit's tree.
		/// </summary>
		public RevTree Tree
		{
			get { return _tree; }
		}

		/// <summary>
		/// Gets the number of parent commits listed in this commit.
		/// </summary>
		public int ParentCount
		{
			get { return Parents.Length; }
		}

    	internal override void parse(RevWalk walk)
        {
            ObjectLoader ldr = walk.getRepository().OpenObject(walk.WindowCursor, this);
            if (ldr == null)
            {
				throw new MissingObjectException(this, Constants.TYPE_COMMIT);
            }
            byte[] data = ldr.CachedBytes;
            if (Constants.OBJ_COMMIT != ldr.Type)
            {
				throw new IncorrectObjectTypeException(this, Constants.TYPE_COMMIT);
            }
            parseCanonical(walk, data);
        }

        public void parseCanonical(RevWalk walk, byte[] raw)
        {
			MutableObjectId idBuffer = walk.IdBuffer;
            idBuffer.FromString(raw, 5);
            _tree = walk.lookupTree(idBuffer);

            int ptr = 46;
            if (Parents == null)
            {
                var pList = new RevCommit[1];
                int nParents = 0;
                
				while (true)
                {
                    if (raw[ptr] != (byte)'p') break;
                    idBuffer.FromString(raw, ptr + 7);
                    RevCommit p = walk.lookupCommit(idBuffer);
                    if (nParents == 0)
                    {
                    	pList[nParents++] = p;
                    }
                    else if (nParents == 1)
                    {
                        pList = new[] { pList[0], p };
                        nParents = 2;
                    }
                    else
                    {
                        if (pList.Length <= nParents)
                        {
                            RevCommit[] old = pList;
                            pList = new RevCommit[pList.Length + 32];
                            Array.Copy(old, 0, pList, 0, nParents);
                        }
                        pList[nParents++] = p;
                    }
                    ptr += 48;
                }
                if (nParents != pList.Length)
                {
                    RevCommit[] old = pList;
                    pList = new RevCommit[nParents];
                    Array.Copy(old, 0, pList, 0, nParents);
                }
                Parents = pList;
            }

            // extract time from "committer "
            ptr = RawParseUtils.committer(raw, ptr);
            if (ptr > 0)
            {
                ptr = RawParseUtils.nextLF(raw, ptr, (byte)'>');

                // In 2038 commitTime will overflow unless it is changed to long.
                CommitTime = RawParseUtils.parseBase10(raw, ptr, null);
            }

            _buffer = raw;
            Flags |= PARSED;
        }

        public override int getType()
        {
            return Constants.OBJ_COMMIT;
        }

        public static void carryFlags(RevCommit c, int carry)
        {
            while (true)
            {
                RevCommit[] pList = c.Parents;
                if (pList == null) return;
                int n = pList.Length;
                if (n == 0) return;

                for (int i = 1; i < n; i++)
                {
                    RevCommit p = pList[i];
                    if ((p.Flags & carry) == carry) continue;
                    p.Flags |= carry;
                    carryFlags(p, carry);
                }

                c = pList[0];
                if ((c.Flags & carry) == carry) return;
                c.Flags |= carry;
            }
        }

        /// <summary>
        /// Carry a RevFlag set on this commit to its parents.
		/// <para />
		/// If this commit is parsed, has parents, and has the supplied flag set on
		/// it we automatically add it to the parents, grand-parents, and so on until
		/// an unparsed commit or a commit with no parents is discovered. This
		/// permits applications to force a flag through the history chain when
		/// necessary.
        /// </summary>
		/// <param name="flag">
		/// The single flag value to carry back onto parents.
		/// </param>
        public void carry(RevFlag flag)
        {
            int carry = Flags & flag.Mask;
            if (carry != 0)
            {
            	carryFlags(this, carry);
            }
        }

        /// <summary>
        /// Parse this commit buffer for display.
        /// </summary>
        /// <param name="walk">
        /// revision walker owning this reference.
        /// </param>
        /// <returns>
		/// Parsed commit.
        /// </returns>
        public Commit AsCommit(RevWalk walk)
        {
            return new Commit(walk.getRepository(), this, _buffer);
        }

    	/// <summary>
        /// Get the nth parent from this commit's parent list.
        /// </summary>
        /// <param name="nth">
        /// the specified parent
        /// </param>
        /// <returns>
        /// Parent index to obtain. Must be in the range 0 through
		/// <see cref="ParentCount"/>-1.
        /// </returns>
        /// <exception cref="IndexOutOfRangeException">
        /// An invalid parent index was specified.
        /// </exception>
        public RevCommit GetParent(int nth)
        {
            return Parents[nth];
        }

        /// <summary>
        /// Obtain the raw unparsed commit body (<b>NOTE - THIS IS NOT A COPY</b>).
		/// <para />
		/// This method is exposed only to provide very fast, efficient access to
		/// this commit's message buffer within a RevFilter. Applications relying on
		/// this buffer should be very careful to ensure they do not modify its
		/// contents during their use of it.
		/// </summary>
		/// <remarks>
		/// This property returns the raw unparsed commit body. This is <b>NOT A COPY</b>.
		/// Altering the contents of this buffer may alter the walker's
		/// knowledge of this commit, and the results it produces.
		/// </remarks>
    	public byte[] RawBuffer
    	{
    		get { return _buffer; }
    	}

    	/**
         * Parse the author identity from the raw buffer.
         * <p>
         * This method parses and returns the content of the author line, After
         * taking the commit's character set into account and decoding the author
         * name and email address. This method is fairly expensive and produces a
         * new PersonIdent instance on each invocation. Callers should invoke this
         * method only if they are certain they will be outputting the result, and
         * should cache the return value for as long as necessary to use all
         * information from it.
         * <p>
         * RevFilter implementations should try to use {@link RawParseUtils} to scan
         * the {@link #getRawBuffer()} instead, as this will allow faster evaluation
         * of commits.
         * 
         * @return identity of the author (name, email) and the time the commit was
         *         made by the author; null if no author line was found.
         */
        public PersonIdent getAuthorIdent()
        {
            byte[] raw = _buffer;
            int nameB = RawParseUtils.author(raw, 0);
            if (nameB < 0) return null;
            return RawParseUtils.parsePersonIdent(raw, nameB);
        }

        /**
         * Parse the committer identity from the raw buffer.
         * <p>
         * This method parses and returns the content of the committer line, After
         * taking the commit's character set into account and decoding the committer
         * name and email address. This method is fairly expensive and produces a
         * new PersonIdent instance on each invocation. Callers should invoke this
         * method only if they are certain they will be outputting the result, and
         * should cache the return value for as long as necessary to use all
         * information from it.
         * <p>
         * RevFilter implementations should try to use {@link RawParseUtils} to scan
         * the {@link #getRawBuffer()} instead, as this will allow faster evaluation
         * of commits.
         * 
         * @return identity of the committer (name, email) and the time the commit
         *         was made by the committer; null if no committer line was found.
         */
        public PersonIdent getCommitterIdent()
        {
            byte[] raw = _buffer;
            int nameB = RawParseUtils.committer(raw, 0);
            if (nameB < 0) return null;
            return RawParseUtils.parsePersonIdent(raw, nameB);
        }

        /// <summary>
        /// Parse the complete commit message and decode it to a string.
		/// <para />
		/// This method parses and returns the message portion of the commit buffer,
		/// After taking the commit's character set into account and decoding the
		/// buffer using that character set. This method is a fairly expensive
		/// operation and produces a new string on each invocation.
        /// </summary>
        /// <returns>
		/// Decoded commit message as a string. Never null.
        /// </returns>
        public string getFullMessage()
        {
            byte[] raw = _buffer;
            int msgB = RawParseUtils.commitMessage(raw, 0);
            if (msgB < 0) return string.Empty;
            Encoding enc = RawParseUtils.parseEncoding(raw);
            return RawParseUtils.decode(enc, raw, msgB, raw.Length);
        }

        /**
         * Parse the commit message and return the first "line" of it.
         * <p>
         * The first line is everything up to the first pair of LFs. This is the
         * "oneline" format, suitable for output in a single line display.
         * <p>
         * This method parses and returns the message portion of the commit buffer,
         * After taking the commit's character set into account and decoding the
         * buffer using that character set. This method is a fairly expensive
         * operation and produces a new string on each invocation.
         * 
         * @return decoded commit message as a string. Never null. The returned
         *         string does not contain any LFs, even if the first paragraph
         *         spanned multiple lines. Embedded LFs are converted to spaces.
         */
        public string getShortMessage()
        {
            byte[] raw = _buffer;
            int msgB = RawParseUtils.commitMessage(raw, 0);
            if (msgB < 0)
                return "";

            Encoding enc = RawParseUtils.parseEncoding(raw);
            int msgE = RawParseUtils.endOfParagraph(raw, msgB);
            string str = RawParseUtils.decode(enc, raw, msgB, msgE);
            if (hasLF(raw, msgB, msgE))
                str = str.Replace('\n', ' ');
            return str;
        }

        public static bool hasLF(byte[] r, int b, int e)
        {
            while (b < e)
            {
            	if (r[b++] == (byte)'\n') return true;
            }
            return false;
        }

        /**
         * Reset this commit to allow another RevWalk with the same instances.
         * <p>
         * Subclasses <b>must</b> call <code>base.reset()</code> to ensure the
         * basic information can be correctly cleared out.
         */
        public void reset()
        {
            InDegree = 0;
        }

        public override void dispose()
        {
            Flags &= ~PARSED;
            _buffer = null;
        }

        public override string ToString()
        {
            var s = new StringBuilder();
            s.Append(Constants.typeString(getType()));
            s.Append(' ');
            s.Append(GetType().Name);
            s.Append(' ');
            s.Append(CommitTime);
            s.Append(' ');
            appendCoreFlags(s);
            return s.ToString();
        }
    }
}