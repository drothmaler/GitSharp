﻿/*
 * Copyright (C) 2008, Shawn O. Pearce <spearce@spearce.org>
 * Copyright (C) 2009, Rolenun <rolenun@gmail.com>
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
using System.Collections.Generic;
using System.IO;
using System.Text;
using GitSharp;
using GitSharp.RevWalk;
using NDesk.Options;

namespace GitSharp.CLI
{

/// <summary>
/// Abstract command which can be invoked from the command line.
/// 
/// Commands are configured with a single "current" repository and then the
/// execute(String[]) method is invoked with the arguments that appear
/// after the subcommand.
/// 
/// Command constructors should perform as little work as possible as they may be
/// invoked very early during process loading, and the command may not execute
/// even though it was constructed.
/// </summary>
public abstract class TextBuiltin 
{
    /// <summary>
    /// Name of the command in use
    /// </summary>
	private String commandName;

    /// <summary>
    /// Website address of the command help file
    /// </summary>
    private String commandHelp;

	/// <summary>
    /// Stream to output to, typically this is standard output
    /// </summary>
	protected StreamWriter streamOut;

	/// <summary>
    /// Git repository the command was invoked within.
	/// </summary>
	protected Repository db;

	/// <summary>
    /// Directory supplied via --git-dir command line option.
	/// </summary>
    protected String gitdir;

	/// <summary>
    /// RevWalk used during command line parsing, if it was required.
	/// </summary>
    protected GitSharp.RevWalk.RevWalk argWalk;

    /// <summary>
    /// Contains the remaining arguments after the options listed in the command line.
    /// </summary>
    public List<String> arguments = new List<String>();

    /// <summary>
    /// Custom OptionSet to allow special option handling rules such as --option dir
    /// </summary>
    public static CmdParserOptionSet options;

    /// <summary>
    /// Used by CommandCatalog and CommandRef to set the command name during initial creation.
    /// </summary>
    /// <param name="name">The command name.</param>
	public void setCommandName(String name) {
		commandName = name;
	}

    /// <summary>
    /// Used by CommandRef to get the command name during initial creation.
    /// </summary>
    public string getCommandName()
    {
        return commandName;
    }

    /// <summary>
    /// Used by CommandCatalog and CommandRef to set the website address of the command help during initial creation.
    /// </summary>
    /// <param name="cmdHelp">The website address of the command help.</param>
    internal void setCommandHelp(String cmdHelp)
    {
        commandHelp = cmdHelp;
    }

    /// <summary>
    /// Used by CommandRef to get the command help website during initial creation.
    /// </summary>
    public string getCommandHelp()
    {
        return commandHelp;
    }

    /// <summary>
    /// Determines if a repository is required.
    /// </summary>
    /// <returns>Returns true if a repository is required.</returns>
	public virtual bool RequiresRepository() {
		return false;
	}

    /// <summary>
    /// Initializes a command for use including the repository and output support.
    /// </summary>
    /// <param name="repo">Specifies the repository to use.</param>
    /// <param name="gitDirectory">Specifies the git directory.</param>
	public void Init(Repository repo, String gitDirectory) {
		try {

#if ported
			String outputEncoding = repo != null ? repo.Config.getString("i18n", null, "logOutputEncoding") : null;

			if (outputEncoding != null)
            {
                streamOut = new StreamWriter(Console.OpenStandardOutput(), outputEncoding);
                Console.SetOut(streamOut);
            }
			else
            {
#endif
            //Initialize the output stream for all console-based messages.
            streamOut = new StreamWriter(Console.OpenStandardOutput());
            Console.SetOut(streamOut);
		} catch (IOException) {
			throw die("cannot create output stream");
		}

        // Initialize the repository in use.
		if (repo != null) {
			db = repo;
			gitdir = repo.Directory.FullName;
		} else {
			db = null;
			gitdir = gitDirectory;
		}
	}

	
	/// <summary>
	/// Parses the command line and runs the corresponding subcommand 
	/// </summary>
    /// <param name="args">Specifies the command line arguments passed after the command name.</param>
    public void Execute(String[] args) {
		Run(args);
	}

    /// <summary>
    /// Parses the options for all subcommands and executes the corresponding code for each option.
    /// </summary>
    /// <param name="args">Specifies the string from the options to the end of the command line.</param>
    /// <returns>Returns the arguments remaining after the options on the command line. Often, these are directories or paths.</returns>
    public List<String> ParseOptions(string[] args)
    {
        try
        {
            arguments = options.Parse(args);
        }
        catch (OptionException err)
        {
            throw die("fatal: " + err.Message);
        }

        return arguments;
    }

    /// <summary>
    /// Returns the current command for lightweight referencing.
    /// </summary>
    /// <returns>Returns this command.</returns>
    public Command GetCommand()
    {
        Type type = Type.GetType(this.ToString());
        object[] attributes = type.GetCustomAttributes(true);
        foreach (object attribute in attributes)
        {
            Command com = attribute as Command;
            if (com != null)
                return com;
        }
        return null;
    }

	/// <summary>
	/// Provides an abstract layer to perform the action of this command.
    /// This method should only be invoked by the  Execute(String[] args) method.
    /// </summary>
	public abstract void Run(String[] args);

    /// <summary>
    /// Opens the default webbrowser to display the command specific help.
    /// </summary>
    /// <param name="url">Specifies the web address to navigate to.</param>
    public void OnlineHelp()
    {
        if (commandHelp.Length > 0)
        {
            Console.WriteLine("Launching default browser to display HTML ...");
            System.Diagnostics.Process.Start(commandHelp);
        }
        else
        {
            Console.WriteLine("There is no online help available for this command.");
        }
    }

    
	/// <summary>
	/// Returns the repository this command accesses.
	/// </summary>
	public Repository GetRepository() {
		return db;
	}

	ObjectId Resolve(String s) {
		ObjectId r = db.Resolve(s);
		if (r == null)
			throw die("Not a revision: " + s);
		return r;
	}

	/// <summary>
	/// Generic method used to return an exception during fatal conditions. 
	/// </summary>
    /// <param name="why">Specifies the textual explanation of why the exception was thrown.</param>
    /// <returns>Returns a runtime exception the caller is expected to throw.</returns>
    protected static Die die(String why) {
		return new Die(why);
	}

	public string AbbreviateRef(String dst, bool abbreviateRemote) {
        if (dst.StartsWith(Constants.R_HEADS))
			dst = dst.Substring(Constants.R_HEADS.Length);
        else if (dst.StartsWith(Constants.R_TAGS))
            dst = dst.Substring(Constants.R_TAGS.Length);
        else if (abbreviateRemote && dst.StartsWith(Constants.R_REMOTES))
            dst = dst.Substring(Constants.R_REMOTES.Length);
		return dst;
	}
}
}